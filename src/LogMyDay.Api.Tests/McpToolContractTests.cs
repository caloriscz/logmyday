using System.Reflection;
using System.Text.RegularExpressions;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp;
using LogMyDay.Mcp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Guard rails over every MCP tool and prompt in LogMyDay.Mcp. Each later tool task runs against
/// these: a tool that forgets an annotation, a policy, a sentinel parameter or its registration
/// fails here before it can reach an agent.
/// </summary>
public class McpToolContractTests
{
    private static readonly Assembly McpAssembly = typeof(McpSchemaVersion).Assembly;

    /// <summary>Tools the design guards with a confirm sentinel; each must take a <c>confirm</c> argument.</summary>
    private static readonly string[] SentinelTools =
    [
        "delete_tag", "delete_unit", "delete_todo_list", "admin_delete_user",
        "restore_secure_backup", "clear_user_data", "admin_clear_all_data", "purge_event_log"
    ];

    private static readonly Regex SnakeCaseVerbNoun = new("^[a-z]+(_[a-z0-9]+)+$", RegexOptions.Compiled);
    private static readonly Regex SnakeCaseToken = new(@"\b[a-z]+(_[a-z0-9]+)+\b", RegexOptions.Compiled);

    private sealed record ToolMethod(Type Type, MethodInfo Method, McpServerToolAttribute Attribute, McpServerTool Tool)
    {
        public string Name => Tool.ProtocolTool.Name;
    }

    private static IReadOnlyList<ToolMethod> DeclaredTools()
    {
        var tools = new List<ToolMethod>();
        foreach (var type in McpAssembly.GetTypes().Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                var options = new McpServerToolCreateOptions { SerializerOptions = McpJson.Options };
                var tool = method.IsStatic
                    ? McpServerTool.Create(method, target: null, options)
                    : McpServerTool.Create(method, _ => throw new InvalidOperationException("not invoked"), options);
                tools.Add(new ToolMethod(type, method, attribute, tool));
            }
        }

        return tools;
    }

    private static IReadOnlyList<McpServerTool> RegisteredTools()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogMyDayMcp();

        using var provider = services.BuildServiceProvider();

        return provider.GetServices<McpServerTool>().ToList();
    }

    private static IEnumerable<AuthorizeAttribute> Policies(ToolMethod tool)
    {
        return tool.Method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(tool.Type.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void ThereAreTools()
    {
        Assert.NotEmpty(DeclaredTools());
    }

    [Fact]
    public void EveryTool_HasAnExplicitSnakeCaseVerbNounName()
    {
        foreach (var tool in DeclaredTools())
        {
            Assert.False(string.IsNullOrEmpty(tool.Attribute.Name), $"{tool.Type.Name}.{tool.Method.Name} relies on the generated name");
            Assert.True(SnakeCaseVerbNoun.IsMatch(tool.Name), $"{tool.Name} is not snake_case verb_noun");
        }
    }

    [Fact]
    public void ToolNames_AreUnique()
    {
        var duplicates = DeclaredTools().GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryTool_SetsAllFourAnnotationsExplicitly()
    {
        // The SDK defaults DestructiveHint to true and leaves the others unset; an agent reading
        // defaults would refuse harmless writes and trust nothing about reads.
        foreach (var tool in DeclaredTools())
        {
            var annotations = tool.Tool.ProtocolTool.Annotations;
            Assert.True(annotations != null, $"{tool.Name} has no annotations");
            Assert.True(annotations.ReadOnlyHint.HasValue, $"{tool.Name} leaves ReadOnly unset");
            Assert.True(annotations.DestructiveHint.HasValue, $"{tool.Name} leaves Destructive unset");
            Assert.True(annotations.IdempotentHint.HasValue, $"{tool.Name} leaves Idempotent unset");
            Assert.True(annotations.OpenWorldHint.HasValue, $"{tool.Name} leaves OpenWorld unset");
            Assert.False(annotations.OpenWorldHint, $"{tool.Name} claims to be open-world; LogMyDay tools only touch LogMyDay data");
        }
    }

    [Fact]
    public void EveryTool_HasADescription()
    {
        foreach (var tool in DeclaredTools())
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Tool.ProtocolTool.Description), $"{tool.Name} has no description");
        }
    }

    [Fact]
    public void EveryTool_RequiresAtLeastTheReadPolicy()
    {
        foreach (var tool in DeclaredTools())
        {
            var policies = Policies(tool).Select(a => a.Policy).ToList();

            Assert.True(policies.Any(p => p is McpPolicies.Read or McpPolicies.Write or McpPolicies.Admin),
                $"{tool.Name} carries no MCP policy at method or class level");
        }
    }

    [Fact]
    public void ReadOnlyFalseTools_RequireTheWriteOrAdminPolicy()
    {
        // The call filter also refuses a read-only key, but the policy is what hides the tool from
        // tools/list — both must agree.
        foreach (var tool in DeclaredTools().Where(t => t.Tool.ProtocolTool.Annotations?.ReadOnlyHint == false))
        {
            var methodPolicies = tool.Method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();

            Assert.True(methodPolicies.Any(p => p is McpPolicies.Write or McpPolicies.Admin),
                $"{tool.Name} changes data but is not [Authorize(Policy = McpWrite|McpAdmin)] on the method");
        }
    }

    [Fact]
    public void ReadOnlyTrueTools_AreNotDestructive()
    {
        foreach (var tool in DeclaredTools().Where(t => t.Tool.ProtocolTool.Annotations?.ReadOnlyHint == true))
        {
            Assert.False(tool.Tool.ProtocolTool.Annotations!.DestructiveHint, $"{tool.Name} is read-only yet marked destructive");
        }
    }

    [Fact]
    public void AdminPolicy_AndAdminPrefix_GoTogether()
    {
        foreach (var tool in DeclaredTools())
        {
            var isAdminPolicy = Policies(tool).Any(a => a.Policy == McpPolicies.Admin);
            var isAdminName = tool.Name.StartsWith("admin_", StringComparison.Ordinal);

            Assert.True(isAdminPolicy == isAdminName,
                $"{tool.Name}: admin_ prefix ({isAdminName}) and McpAdmin policy ({isAdminPolicy}) must match");
        }
    }

    [Fact]
    public void SentinelTools_TakeAConfirmArgument_AndAreDestructive()
    {
        foreach (var tool in DeclaredTools().Where(t => SentinelTools.Contains(t.Name)))
        {
            Assert.True(tool.Method.GetParameters().Any(p => p.Name == "confirm" && p.ParameterType == typeof(string)),
                $"{tool.Name} is sentinel-guarded by design but has no string confirm parameter");
            Assert.True(tool.Tool.ProtocolTool.Annotations?.DestructiveHint, $"{tool.Name} must be marked destructive");
            Assert.Contains("confirm", tool.Tool.ProtocolTool.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EveryDeclaredToolType_IsRegisteredInAddLogMyDayMcp()
    {
        // Tools are registered per type on purpose; this catches a new type that was never listed.
        var declared = DeclaredTools().Select(t => t.Name).OrderBy(n => n).ToList();
        var registered = RegisteredTools().Select(t => t.ProtocolTool.Name).OrderBy(n => n).ToList();

        Assert.Equal(declared, registered);
    }

    [Fact]
    public void ServerInstructions_AndPrompts_NameOnlyRegisteredTools()
    {
        var known = DeclaredTools().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var texts = new List<(string Source, string Text)>
        {
            ("ServerInstructions", McpServiceCollectionExtensions.ServerInstructions)
        };

        // Prompt wording lives in string constants on the prompt types; every snake_case token in
        // them is taken as a tool reference and must resolve.
        foreach (var type in McpAssembly.GetTypes().Where(t => t.GetCustomAttribute<McpServerPromptTypeAttribute>() != null))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(string) && (f.IsLiteral || f.IsInitOnly)))
            {
                texts.Add(($"{type.Name}.{field.Name}", (string?)field.GetValue(null) ?? string.Empty));
            }
        }

        foreach (var (source, text) in texts)
        {
            foreach (Match match in SnakeCaseToken.Matches(text))
            {
                Assert.True(known.Contains(match.Value), $"{source} mentions '{match.Value}', which is not a registered tool");
            }
        }
    }
}
