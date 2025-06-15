# LogMyDay Backup & Restore System

## Overview

The LogMyDay backup and restore system provides a simple, database-independent way to export, import, and manage your application data using JSON files. This is particularly useful during development when the database structure may change frequently.

## Features

### 1. Export Data
- **Endpoint**: `GET /api/backup/export`
- **UI**: Backup page (`/backup`) → Export Data section
- **Functionality**: Exports all tags, activities, input types, and patterns to a JSON file
- **Optional Parameters**: 
  - `userId`: Filter data for specific user (optional)
- **Output**: Downloads a timestamped JSON file (e.g., `logmyday-backup-2025-06-15-10-15-30.json`)

### 2. Import Data
- **Endpoint**: `POST /api/backup/import`
- **UI**: Backup page (`/backup`) → Import Data section
- **Functionality**: Imports data from JSON backup file, avoiding duplicates
- **Parameters**:
  - `file`: JSON backup file (required)
  - `clearExisting`: Whether to clear existing data before import (optional, default: false)
  - `userId`: Associate imported data with specific user (optional)
- **Features**:
  - Duplicate prevention: Skips existing records based on unique identifiers
  - Data validation: Validates file format and referential integrity
  - Transaction safety: All-or-nothing import with rollback on errors
  - Detailed statistics: Reports imported/skipped counts for each entity type

### 3. Validate Backup
- **Endpoint**: `POST /api/backup/validate`
- **UI**: Backup page (`/backup`) → Validate button
- **Functionality**: Validates backup file format without importing
- **Checks**:
  - JSON format validity
  - Required fields presence
  - Referential integrity (tags reference valid input types/patterns)
  - Duplicate detection
  - Data consistency

### 4. Clear Data
- **Endpoint**: `DELETE /api/backup/clear`
- **UI**: Backup page (`/backup`) → Danger Zone
- **Functionality**: Clears all data from the database
- **Parameters**:
  - `userId`: Clear data for specific user only (optional)
- **Safety**: Requires confirmation ("DELETE" text input)

### 5. Backup Info
- **Endpoint**: `GET /api/backup/info`
- **UI**: Automatically loaded on backup page
- **Functionality**: Shows current database statistics
- **Data**: Count of tags, activities, input types, and patterns

## JSON Structure

The backup JSON file has the following structure:

```json
{
  "metadata": {
    "exportDate": "2025-06-15T10:15:30.123Z",
    "version": "1.0",
    "totalTags": 5,
    "totalActivities": 150,
    "totalInputTypes": 3,
    "totalPatterns": 2
  },
  "inputTypes": [
    {
      "name": "Text Input"
    }
  ],
  "patterns": [
    {
      "name": "Email Pattern",
      "patternValue": "^[\\w\\.-]+@[\\w\\.-]+\\.[a-zA-Z]{2,}$",
      "description": "Email validation pattern"
    }
  ],
  "tags": [
    {
      "tagName": "Work",
      "inputTypeName": "Text Input",
      "isRequired": false,
      "timeGranularity": "Exact",
      "isRepeatable": true,
      "isRange": false,
      "patternName": null,
      "userId": "12345678-1234-1234-1234-123456789012"
    }
  ],
  "activities": [
    {
      "dateCreated": "2025-06-15T08:00:00Z",
      "dateStarted": "2025-06-15T09:00:00Z",
      "dateFinished": "2025-06-15T10:00:00Z",
      "description": "Morning standup meeting",
      "tagName": "Work",
      "userId": "12345678-1234-1234-1234-123456789012"
    }
  ]
}
```

## Usage Scenarios

### 1. Development Backup
During development, use the export feature to backup your test data before making database schema changes:

1. Navigate to `/backup`
2. Click "Export Data"
3. Save the downloaded JSON file
4. After schema changes, use "Clear All Data" (if needed) and "Import Data"

### 2. Environment Migration
Move data between development, staging, and production environments:

1. Export from source environment
2. Import to target environment (optionally clearing existing data)

### 3. Data Recovery
Recover from accidental data loss:

1. Use a recent backup file
2. Clear existing data (if corrupted)
3. Import the backup

### 4. Selective Data Management
- Export without `userId` to get all data
- Import with `userId` to associate data with specific user
- Clear with `userId` to remove only specific user's data

## Security Considerations

- The backup endpoints are part of the API and should be protected by authentication in production
- Backup files may contain sensitive user data - handle appropriately
- The clear data operation is irreversible - use with caution
- Consider implementing additional authorization for backup operations in production

## Import Process Order

The import process follows a specific order to maintain referential integrity:

1. **Input Types** - Referenced by tags
2. **Patterns** - Referenced by tags  
3. **Tags** - Referenced by activities
4. **Activities** - Reference tags

Duplicates are detected and skipped based on:
- Input Types: Name
- Patterns: Name
- Tags: TagName (within user scope)
- Activities: Not duplicate-checked (allows multiple activities with same data)

## Error Handling

The system provides comprehensive error reporting:

- **Validation Errors**: Format issues, missing references, duplicates
- **Import Errors**: Database constraint violations, transaction failures
- **Warning Messages**: Non-fatal issues like unknown references (activities referencing non-existent tags)

All operations return detailed status information including:
- Success/failure status
- Detailed error messages
- Import statistics (imported/skipped counts)
- Warning messages for non-critical issues
