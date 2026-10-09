using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_WhenStatusIsInProgress_ReturnsOnlyInProgressTickets()
    {
        // Arrange: keep the in-memory SQLite database open for this test.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var inProgress = Ticket.Raise(
            "TCK-1001", "Login issue", "Cannot log in to the portal.",
            customer.Id, category.Id, TicketPriority.High, now);
        inProgress.ChangeStatus(TicketStatus.InProgress, now);

        var newTicket = Ticket.Raise(
            "TCK-1002", "Payment issue", "Payment is not working.",
            customer.Id, category.Id, TicketPriority.Medium, now);

        db.AddRange(inProgress, newTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        // Act
        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                Status = TicketStatus.InProgress
            },
            CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-1001", result.Items[0].Reference);
        Assert.Equal(TicketStatus.InProgress, result.Items[0].Status);
        Assert.Equal(1, result.TotalCount);
    }


    
    [Fact]
    public async Task GetPagedAsync_WhenPriorityIsHigh_ReturnsOnlyHighPriorityTickets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var highPriority = Ticket.Raise(
            "TCK-2001", "Urgent login issue", "Cannot log in.",
            customer.Id, category.Id, TicketPriority.High, now);

        var mediumPriority = Ticket.Raise(
            "TCK-2002", "General question", "Need some information.",
            customer.Id, category.Id, TicketPriority.Medium, now);

        db.AddRange(highPriority, mediumPriority);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                Priority = TicketPriority.High
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-2001", result.Items[0].Reference);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
    }

    
    [Fact]
    public async Task GetPagedAsync_WhenStatusAndPriorityAreProvided_ReturnsTicketsMatchingBoth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var matchingTicket = Ticket.Raise(
            "TCK-3001", "Urgent login issue", "Cannot log in.",
            customer.Id, category.Id, TicketPriority.High, now);
        matchingTicket.ChangeStatus(TicketStatus.InProgress, now);

        var wrongStatus = Ticket.Raise(
            "TCK-3002", "High priority new ticket", "Needs attention.",
            customer.Id, category.Id, TicketPriority.High, now);

        var wrongPriority = Ticket.Raise(
            "TCK-3003", "Medium priority in progress", "Work is ongoing.",
            customer.Id, category.Id, TicketPriority.Medium, now);
        wrongPriority.ChangeStatus(TicketStatus.InProgress, now);

        db.AddRange(matchingTicket, wrongStatus, wrongPriority);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-3001", result.Items[0].Reference);
        Assert.Equal(TicketStatus.InProgress, result.Items[0].Status);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
        Assert.Equal(1, result.TotalCount);
    }


    [Fact]
    public async Task GetPagedAsync_WhenSearchingCustomerName_ReturnsMatchingCustomerTickets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Blue Ocean Travel", "blueocean@example.com", null, CustomerTier.Premium, now);
        var otherCustomer = new Customer(
            "Green Valley Tours", "greenvalley@example.com", null, CustomerTier.Standard, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, otherCustomer, category);
        await db.SaveChangesAsync();

        var matchingTicket = Ticket.Raise(
            "TCK-4001", "Booking question", "Need help with a booking.",
            customer.Id, category.Id, TicketPriority.Medium, now);

        var otherTicket = Ticket.Raise(
            "TCK-4002", "Payment question", "Need help with payment.",
            otherCustomer.Id, category.Id, TicketPriority.Medium, now);

        db.AddRange(matchingTicket, otherTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                Search = "  Blue Ocean  "
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-4001", result.Items[0].Reference);
        Assert.Equal("Blue Ocean Travel", result.Items[0].Customer.Name);
        Assert.Equal(1, result.TotalCount);
    }


      
    [Fact]
    public async Task GetPagedAsync_WhenUnassignedOnlyIsTrue_ReturnsOnlyUnassignedTickets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, now);
        var category = new Category("General", false, false);
        var agent = new Agent("Alex Turner", "alex@example.com", 10, now);

        db.AddRange(customer, category, agent);
        await db.SaveChangesAsync();

        var unassignedTicket = Ticket.Raise(
            "TCK-5001", "Unassigned issue", "Waiting for an owner.",
            customer.Id, category.Id, TicketPriority.Medium, now);

        var assignedTicket = Ticket.Raise(
            "TCK-5002", "Assigned issue", "Already has an owner.",
            customer.Id, category.Id, TicketPriority.Medium, now);
        assignedTicket.AssignTo(agent.Id, now);

        db.AddRange(unassignedTicket, assignedTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                UnassignedOnly = true
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("TCK-5001", result.Items[0].Reference);
        Assert.Null(result.Items[0].AssignedAgent);
        Assert.Equal(1, result.TotalCount);
    }

   
    [Fact]
    public async Task GetPagedAsync_WhenFilteredAndPaged_ReturnsCorrectItemsAndTotalCount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;
        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null, CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
        {
            var ticket = Ticket.Raise(
                $"TCK-60{i:D2}", $"Issue {i}", "Test ticket description.",
                customer.Id, category.Id,
                i <= 3 ? TicketPriority.High : TicketPriority.Medium,
                now.AddMinutes(i));

            db.Add(ticket);
        }

        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new SupportDesk.Application.Contracts.Tickets.TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 2,
                PageSize = 2,
                SortDirection = "asc"
            },
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.All(result.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }


}
