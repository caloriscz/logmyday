using System.Text.Json;
using LogMyDay.App.Mobile.Models;

// Test QuickActivityButton serialization/deserialization
var button = new QuickActivityButton
{
    Id = 1,
    Name = "Test Button",
    TagId = 1,
    TagName = "Test Tag",
    Value = "Test Value",
    CreatedAt = DateTime.Now,
    LastUsed = null,
    IsEnabled = true
};

Console.WriteLine("Testing QuickActivityButton serialization...");

try
{
    // Test serialization
    var json = JsonSerializer.Serialize(button);
    Console.WriteLine($"✅ Serialization successful: {json}");
    
    // Test deserialization
    var deserialized = JsonSerializer.Deserialize<QuickActivityButton>(json);
    Console.WriteLine($"✅ Deserialization successful: {deserialized?.Name}");
    
    // Test creating with empty strings (default values)
    var buttonWithDefaults = new QuickActivityButton
    {
        Id = 2,
        TagId = 2
        // Name and TagName should use default empty strings
    };
    
    Console.WriteLine($"✅ Default creation successful: Name='{buttonWithDefaults.Name}', TagName='{buttonWithDefaults.TagName}'");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
