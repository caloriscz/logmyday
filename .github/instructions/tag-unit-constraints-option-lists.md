# Tag Metadata Enhancements Assignment

## Objective
Extend the tag subsystem to support optional suffix units, numeric constraints, and reusable option lists across the stack.

## Scope
1. **Domain Model & Persistence**
   - Add `Quantity`, `Unit`, `TagOptionList`, and `TagOption` entities.
   - Extend `Tag` with nullable fields for unit association, numeric constraints, default value, and option list reference.
   - Update `LogMyDayDbContext` with the required `DbSet` properties and relationship configuration.
   - Create EF Core migrations covering the new tables and columns.

2. **DTOs & Backup Formats**
   - Expand `TagRequest`, `TagResponse`, and `TagBackup` with unit, numeric constraint, default value, and option list fields.

3. **Application Services & Queries**
   - Update `TagService` CRUD operations to map the new fields, eagerly load related entities, and surface unit symbols and option list names.

4. **Validation & Business Rules**
   - Enforce numeric constraints where applicable and prevent conflicting option list + numeric constraint configurations.

5. **Testing & Documentation**
   - Add/extend unit tests around the enhanced behaviors and document usage patterns for the new metadata.

## Acceptance Criteria
- Database schema includes the new entities/columns without data loss.
- API endpoints accept and return the expanded tag metadata.
- Server-side validation enforces numeric constraints and option list rules.
- Automated tests cover the added functionality.
