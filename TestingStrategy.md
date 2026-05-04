# KulturHub Project

## Stack

- **API**: ASP.NET (C#) in JetBrains Rider
- **ORM**: Dapper
- **Database**: PostgreSQL
- **Architecture**: Clean Architecture: Api, Application, Domain, Infrastructure

---

## Project Structure

```
KulturHub.sln
├── KulturHub.Api/
├── KulturHub.Application/
├── KulturHub.Domain/
├── KulturHub.Infrastructure/
└── KulturHub.UnitTests/
```

---

## Testing Strategy

### Unit Tests (`KulturHub.UnitTests`)

**Goal**: Test handler logic in isolation, without a database or HTTP.

**Tools**:
- `xUnit` (test framework)
- `Moq` (mocking)
- `FluentAssertions` (readable assertions)

**Rules**:
- Each handler gets its own test class: `{HandlerName}Tests.cs`
- Repositories are always mocked (never a real DB)
- Per test class: list all rules as a comment first, then write the tests

**Naming Convention**:
```
MethodName_Scenario_ExpectedResult
Handle_WhenBirthDateIsInFuture_ShouldReturnFailure
Handle_WhenAllInputsAreValid_ShouldCallRepository
```

**Required Test Cases per Handler**:
1. Happy Path (valid inputs -> success, repository is called)
2. Each validation rule as its own failure case
3. Edge cases (null, empty, boundary values)

**Example Structure**:
```csharp
public class AddPersonHandlerTests
{
    // Rules:
    // - First name must not be empty
    // - Birth date must not be in the future
    // - Death date must be after birth date (if provided)

    private readonly Mock<IPersonRepository> _repoMock = new();
    private readonly AddPersonHandler _handler;

    public AddPersonHandlerTests()
    {
        _handler = new AddPersonHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenFirstNameIsEmpty_ShouldReturnFailure()
    {
        /* Test Code */
    }
}
```

---

## When AI (Claude) Generates Tests

### Workflow

When a new handler is created or modified, follow this process:

1. **Read the handler** completely
2. **List all domain rules** as a comment at the top of the test class
3. **Create a test table** (Scenario | Input | Expected Result)
4. **Write the tests** following the naming convention above
5. **Ensure**: the happy path is always included

---

## Coding Conventions

- Language: Everything in English
- Async/Await throughout handlers and repositories
- Result pattern for error handling in handlers (no exception throwing for business errors)