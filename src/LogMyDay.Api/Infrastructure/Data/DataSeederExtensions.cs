using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Infrastructure.Data;

public static class DataSeederExtensions
{
    public static void SeedData(this ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<InputType>().HasData(
            new InputType 
            { 
                Id = 1, 
                Name = "Integer", 
                IsRangeEditable = true,
                IsMinimumEditable = true,
                IsMaximumEditable = true,
                IsStepEditable = true,
                IsRepeatableEditable = true,
                Description = "Whole number input (e.g., 1, 42, -5)" 
            },
            new InputType 
            { 
                Id = 2, 
                Name = "String", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = true,
                Description = "Free-form text input" 
            },
            new InputType 
            { 
                Id = 3, 
                Name = "Boolean", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "True/false checkbox input" 
            },
            new InputType 
            { 
                Id = 4, 
                Name = "Date", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Date selection input" 
            },
            new InputType 
            { 
                Id = 5, 
                Name = "Time", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Time selection input" 
            },
            new InputType 
            { 
                Id = 6, 
                Name = "Decimal, precision 2", 
                IsRangeEditable = true,
                IsMinimumEditable = true,
                IsMaximumEditable = true,
                IsStepEditable = true,
                IsRepeatableEditable = true,
                Description = "Decimal number with 2 decimal places (e.g., 3.14, 99.99)" 
            },
            new InputType 
            { 
                Id = 7, 
                Name = "Star Rating 0-5", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Star rating scale from 0 to 5 (higher is better)" 
            },
            new InputType 
            { 
                Id = 8, 
                Name = "Star Rating 0-10", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Star rating scale from 0 to 10 (higher is better)" 
            },
            new InputType 
            { 
                Id = 9, 
                Name = "Percentage", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Percentage value from 0 to 100" 
            },
            new InputType 
            { 
                Id = 10, 
                Name = "Score 0-5", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Score scale from 0 to 5 (lower is better, e.g. pain, severity)" 
            },
            new InputType 
            { 
                Id = 11, 
                Name = "Score 0-10", 
                IsRangeEditable = true,
                IsMinimumEditable = false,
                IsMaximumEditable = false,
                IsStepEditable = false,
                IsRepeatableEditable = false,
                Description = "Score scale from 0 to 10 (lower is better, e.g. pain, severity)" 
            }
        );
    }
}
