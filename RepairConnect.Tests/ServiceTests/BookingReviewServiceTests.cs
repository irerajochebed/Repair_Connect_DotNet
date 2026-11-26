using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;
using RepairConnect.Services.Common;

namespace RepairConnect.Tests.ServiceTests;

public sealed class BookingReviewServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<RepairConnectDbContext> _options;
    private int _plumbingId, _electricalId, _clientId, _otherClientId, _techId, _otherTechId, _pendingTechId;

    public BookingReviewServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<RepairConnectDbContext>().UseSqlite(_connection).Options;
        using var context = CreateContext();
        context.Database.EnsureCreated();
        Seed(context);
    }

    public void Dispose() => _connection.Dispose();

    private RepairConnectDbContext CreateContext() => new SqliteTestDbContext(_options);

    private void Seed(RepairConnectDbContext context)
    {
        var plumbing = new ServiceCategory { Name = "Plumbing" };
        var electrical = new ServiceCategory { Name = "Electrical" };
        context.ServiceCategories.AddRange(plumbing, electrical);
        var client = NewUser("client", UserRoles.Client);
        var otherClient = NewUser("other", UserRoles.Client);
        var tech = NewUser("alice", UserRoles.Client | UserRoles.Technician);
        var otherTech = NewUser("bob", UserRoles.Client | UserRoles.Technician);
        var pendingTech = NewUser("carol", UserRoles.Client);
        context.Users.AddRange(client, otherClient, tech, otherTech, pendingTech);
        context.SaveChanges();

        context.TechnicianProfiles.AddRange(
            NewProfile(tech.Id, "Pipes", VerificationStatus.Approved),
            NewProfile(otherTech.Id, "Wiring", VerificationStatus.Approved),
            NewProfile(pendingTech.Id, "Pipes", VerificationStatus.Pending));
        context.TechnicianCategories.AddRange(
            new TechnicianCategory { TechnicianUserId = tech.Id, CategoryId = plumbing.Id },
            new TechnicianCategory { TechnicianUserId = otherTech.Id, CategoryId = plumbing.Id },
            new TechnicianCategory { TechnicianUserId = otherTech.Id, CategoryId = electrical.Id },
            new TechnicianCategory { TechnicianUserId = pendingTech.Id, CategoryId = plumbing.Id });
        context.SaveChanges();

        (_plumbingId, _electricalId) = (plumbing.Id, electrical.Id);
        (_clientId, _otherClientId, _techId, _otherTechId, _pendingTechId) =
            (client.Id, otherClient.Id, tech.Id, otherTech.Id, pendingTech.Id);
    }

    private static ApplicationUser NewUser(string name, UserRoles roles) => new()
    {
        DisplayName = name, Email = name + "@test.local", NormalizedEmail = (name + "@TEST.LOCAL").ToUpperInvariant(),
        PasswordHash = "hash", Roles = roles, Status = UserStatus.Active, CreatedAt = DateTime.UtcNow
    };

    private static TechnicianProfile NewProfile(int userId, string specialization, VerificationStatus status) => new()
    {
        UserId = userId, Specialization = specialization, YearsOfExperience = 3, VerificationStatus = status,
        CreatedAt = DateTime.UtcNow
    };

    private CreateRepairRequest NewRequest(int technicianId, int? categoryId = null) => new()
    {
        CategoryId = categoryId ?? _plumbingId, TechnicianUserId = technicianId, Title = "Leaking sink",
        Description = "The kitchen sink leaks under the cabinet.", ServiceLocation = "Kigali", Urgency = RepairUrgency.High
    };

    private static ClientBookingService Bookings(RepairConnectDbContext context) =>
        new(context, new RequestService(context));

    private async Task<RepairRequestVm> CreateRequestAsync(int technicianId)
    {
        using var context = CreateContext();
        var result = await new RequestService(context).CreateAsync(_clientId, NewRequest(technicianId));
        Assert.True(result.Succeeded, result.Error);
        return result.Data!;
    }

    private async Task SetBookingStatusAsync(int bookingId, BookingStatus status)
    {
        using var context = CreateContext();
        var booking = await context.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        if (status == BookingStatus.Completed) booking.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    // ---------- Request + booking ----------

    [Fact]
    public async Task CreateRequest_SavesRequestAndPendingBookingTogether()
    {
        var request = await CreateRequestAsync(_techId);

        Assert.Equal(RepairRequestStatus.Open, request.Status);
        var booking = Assert.Single(request.Bookings);
        Assert.Equal(_techId, booking.TechnicianUserId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal("alice", booking.TechnicianName);
    }

    [Fact]
    public async Task CreateRequest_BookingYourself_IsRejected_AndNothingIsSaved()
    {
        using var context = CreateContext();
        var result = await new RequestService(context).CreateAsync(_techId, NewRequest(_techId));

        Assert.False(result.Succeeded);
        Assert.Contains("yourself", result.Error);
        Assert.Equal(0, await context.RepairRequests.CountAsync());
        Assert.Equal(0, await context.Bookings.CountAsync());
    }

    [Fact]
    public async Task CreateRequest_UnapprovedTechnician_IsRejected()
    {
        using var context = CreateContext();
        var result = await new RequestService(context).CreateAsync(_clientId, NewRequest(_pendingTechId));

        Assert.False(result.Succeeded);
        Assert.Equal(0, await context.RepairRequests.CountAsync());
    }

    [Fact]
    public async Task CreateRequest_TechnicianWithoutThatService_IsRejected()
    {
        using var context = CreateContext();
        var result = await new RequestService(context).CreateAsync(_clientId, NewRequest(_techId, _electricalId));

        Assert.False(result.Succeeded);
        Assert.Contains("does not offer", result.Error);
    }

    [Fact]
    public async Task CreateRequest_MissingTitle_IsRejected()
    {
        using var context = CreateContext();
        var request = NewRequest(_techId);
        request.Title = "  ";

        var result = await new RequestService(context).CreateAsync(_clientId, request);

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task GetRequest_OtherClient_SeesNotFound()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();

        var result = await new RequestService(context).GetByIdAsync(_otherClientId, request.Id);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ChooseAnotherTechnician_WhileBookingIsActive_IsRejected()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();

        var result = await Bookings(context).ChooseTechnicianAsync(_clientId, request.Id, _otherTechId);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal(1, await context.Bookings.CountAsync());
    }

    [Fact]
    public async Task Cancel_ThenChooseAnotherTechnician_LeavesOneActiveBooking()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();
        var service = Bookings(context);

        var cancelled = await service.CancelAsync(_clientId, request.Bookings[0].Id);
        Assert.True(cancelled.Succeeded, cancelled.Error);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Data!.Bookings[0].Status);

        var rebooked = await service.ChooseTechnicianAsync(_clientId, request.Id, _otherTechId);
        Assert.True(rebooked.Succeeded, rebooked.Error);
        Assert.Equal(2, rebooked.Data!.Bookings.Count);
        var active = Assert.Single(rebooked.Data.Bookings, b => b.Status == BookingStatus.Pending);
        Assert.Equal(_otherTechId, active.TechnicianUserId);
    }

    [Fact]
    public async Task ChooseAnotherTechnician_AfterDecline_Works()
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Declined);
        using var context = CreateContext();

        var result = await Bookings(context).ChooseTechnicianAsync(_clientId, request.Id, _otherTechId);

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Cancel_ByAnotherClient_IsForbidden()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();

        var result = await Bookings(context).CancelAsync(_otherClientId, request.Bookings[0].Id);

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        Assert.Equal(BookingStatus.Pending, (await context.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancel_CompletedBooking_IsRejected()
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Completed);
        using var context = CreateContext();

        var result = await Bookings(context).CancelAsync(_clientId, request.Bookings[0].Id);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Database_AllowsOnlyOneActiveBookingPerRequest()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();
        context.Bookings.Add(new Booking
        {
            RepairRequestId = request.Id, TechnicianUserId = _otherTechId, Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // ---------- Reviews ----------

    [Fact]
    public async Task Review_BeforeCompletion_IsRejected()
    {
        var request = await CreateRequestAsync(_techId);
        using var context = CreateContext();

        var result = await new ReviewService(context).CreateAsync(_clientId, request.Bookings[0].Id,
            new CreateReviewRequest { Rating = 5 });

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Review_ByAnotherClient_IsForbidden()
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Completed);
        using var context = CreateContext();

        var result = await new ReviewService(context).CreateAsync(_otherClientId, request.Bookings[0].Id,
            new CreateReviewRequest { Rating = 5 });

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public async Task Review_RatingOutsideOneToFive_IsRejected(int? rating)
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Completed);
        using var context = CreateContext();

        var result = await new ReviewService(context).CreateAsync(_clientId, request.Bookings[0].Id,
            new CreateReviewRequest { Rating = rating });

        Assert.Equal(ServiceErrorType.BadRequest, result.ErrorType);
    }

    [Fact]
    public async Task Review_CompletedBooking_OnlyOnce_AndUpdatesAverage()
    {
        var first = await CreateRequestAsync(_techId);
        var second = await CreateRequestAsync(_otherTechId);
        await SetBookingStatusAsync(first.Bookings[0].Id, BookingStatus.Completed);
        // Second request: rebook to alice after cancelling bob, then complete.
        using (var context = CreateContext())
        {
            await Bookings(context).CancelAsync(_clientId, second.Bookings[0].Id);
            var rebooked = await Bookings(context).ChooseTechnicianAsync(_clientId, second.Id, _techId);
            second = rebooked.Data!;
        }
        var secondBookingId = second.Bookings.Single(b => b.Status == BookingStatus.Pending).Id;
        await SetBookingStatusAsync(secondBookingId, BookingStatus.Completed);

        using (var context = CreateContext())
        {
            var reviews = new ReviewService(context);
            var created = await reviews.CreateAsync(_clientId, first.Bookings[0].Id,
                new CreateReviewRequest { Rating = 5, Comment = "  Fast and clean.  " });
            Assert.True(created.Succeeded, created.Error);
            Assert.Equal("Fast and clean.", created.Data!.Comment);

            var duplicate = await reviews.CreateAsync(_clientId, first.Bookings[0].Id, new CreateReviewRequest { Rating = 1 });
            Assert.Equal(ServiceErrorType.Conflict, duplicate.ErrorType);

            Assert.True((await reviews.CreateAsync(_clientId, secondBookingId, new CreateReviewRequest { Rating = 4 })).Succeeded);

            var (average, count) = await reviews.GetRatingAsync(_techId);
            Assert.Equal(4.5, average);
            Assert.Equal(2, count);
            Assert.Equal((null, 0), await reviews.GetRatingAsync(_otherTechId));
        }
    }

    // ---------- Directory ----------

    [Fact]
    public async Task Directory_ListsOnlyApprovedTechnicians_WithFiltersAndPaging()
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Completed);
        using var context = CreateContext();
        await new ReviewService(context).CreateAsync(_clientId, request.Bookings[0].Id, new CreateReviewRequest { Rating = 4 });
        var directory = new TechnicianDirectoryService(context);

        var all = (await directory.SearchAsync(null, null, null, 1, 10)).Data!;
        Assert.Equal(2, all.TotalCount);
        Assert.DoesNotContain(all.Items, t => t.UserId == _pendingTechId);
        Assert.Equal(_techId, all.Items[0].UserId); // rated technicians come first
        Assert.Equal(4.0, all.Items[0].AverageRating);
        Assert.Null(all.Items[1].AverageRating);

        var electrical = (await directory.SearchAsync(null, _electricalId, null, 1, 10)).Data!;
        Assert.Equal(_otherTechId, Assert.Single(electrical.Items).UserId);

        var byName = (await directory.SearchAsync("Wir", null, null, 1, 10)).Data!;
        Assert.Equal(_otherTechId, Assert.Single(byName.Items).UserId);

        var rated = (await directory.SearchAsync(null, null, 4, 1, 10)).Data!;
        Assert.Equal(_techId, Assert.Single(rated.Items).UserId);

        var paged = (await directory.SearchAsync(null, null, null, 2, 1)).Data!;
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.TotalPages);

        Assert.False((await directory.SearchAsync(null, null, null, 0, 10)).Succeeded);
    }

    [Fact]
    public async Task Directory_Profile_ShowsRatingAndReviews_AndHidesUnapproved()
    {
        var request = await CreateRequestAsync(_techId);
        await SetBookingStatusAsync(request.Bookings[0].Id, BookingStatus.Completed);
        using var context = CreateContext();
        await new ReviewService(context).CreateAsync(_clientId, request.Bookings[0].Id,
            new CreateReviewRequest { Rating = 5, Comment = "Great" });
        var directory = new TechnicianDirectoryService(context);

        var profile = (await directory.GetProfileAsync(_techId)).Data!;
        Assert.Equal(5.0, profile.AverageRating);
        Assert.Equal(1, profile.CompletedJobs);
        Assert.Equal("client", Assert.Single(profile.RecentReviews).ClientName);
        Assert.Equal("Plumbing", Assert.Single(profile.Categories).Name);

        Assert.Equal(ServiceErrorType.NotFound, (await directory.GetProfileAsync(_pendingTechId)).ErrorType);
    }
}
