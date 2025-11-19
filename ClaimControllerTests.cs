using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using PROG6212_POE.Controllers;
using PROG6212_POE.Data;
using PROG6212_POE.Models;
using PROG6212_POE.Repositories;
using Xunit;

namespace PROG6212_POE.Tests
{
    public class ClaimControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly AppRepository _repository;
        private readonly ClaimController _controller;

        public ClaimControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _repository = new AppRepository(_context);

            _controller = new ClaimController(_repository);

            // FIX: Add TempData provider (this was causing NullReference!)
            var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
            _controller.TempData = tempData;

            // Setup session
            var session = new MockHttpSession();
            var httpContext = new DefaultHttpContext { Session = session };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        [Fact]
        public async Task Submit_ValidClaimWithCorrectRate_RedirectsToDashboard()
        {
            // Arrange
            var email = "lecturer@test.com";
            _controller.HttpContext.Session.SetString("UserRole", "Lecturer");
            _controller.HttpContext.Session.SetString("UserEmail", email);

            var lecturer = new Lecturer { Email = email, Name = "Test Lecturer", HourlyRate = 500m, Phone = "123" };
            _context.Lecturers.Add(lecturer);
            await _context.SaveChangesAsync();

            var claim = new Claim { HoursWorked = 10, HourlyRate = 500m }; // Correct rate

            // Act
            var result = await _controller.Submit(claim, null);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);

            var savedClaim = await _context.Claims.FirstOrDefaultAsync();
            Assert.NotNull(savedClaim);
            Assert.Equal("Pending", savedClaim.Status);
            Assert.Equal("lecturer@test.com", savedClaim.LecturerId);
        }

        [Fact]
        public async Task Submit_IncorrectHourlyRate_AutoRejectsClaim()
        {
            // Arrange
            var email = "lecturer@test.com";
            _controller.HttpContext.Session.SetString("UserRole", "Lecturer");
            _controller.HttpContext.Session.SetString("UserEmail", email);

            var lecturer = new Lecturer { Email = email, Name = "Test", HourlyRate = 500m, Phone = "123" };
            _context.Lecturers.Add(lecturer);
            await _context.SaveChangesAsync();

            var claim = new Claim { HoursWorked = 10, HourlyRate = 999m }; // Wrong rate!

            // Act
            var result = await _controller.Submit(claim, null);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);

            var savedClaim = await _context.Claims.FirstOrDefaultAsync();
            Assert.NotNull(savedClaim);
            Assert.Equal("Rejected", savedClaim.Status);
            Assert.Contains("AUTO-REJECTED", savedClaim.CoordinatorNotes);
            Assert.Equal(500m, savedClaim.HourlyRate); // Rate was corrected
        }

        [Fact]
        public async Task Verify_ValidPendingClaim_ChangesStatusToVerified()
        {
            _controller.HttpContext.Session.SetString("UserRole", "Coordinator");

            var claim = new Claim { Status = "Pending", LecturerId = "test@test.com" };
            _context.Claims.Add(claim);
            await _context.SaveChangesAsync();

            var result = await _controller.Verify(claim.Id, "Good work");

            var updated = await _context.Claims.FindAsync(claim.Id);
            Assert.Equal("Verified", updated.Status);
            Assert.Equal("Good work", updated.CoordinatorNotes);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("CoordinatorDashboard", redirect.ActionName);
        }

        [Fact]
        public async Task Approve_ValidVerifiedClaim_ChangesToApproved()
        {
            _controller.HttpContext.Session.SetString("UserRole", "Manager");

            var claim = new Claim { Status = "Verified", LecturerId = "test@test.com" };
            _context.Claims.Add(claim);
            await _context.SaveChangesAsync();

            var result = await _controller.Approve(claim.Id);

            var updated = await _context.Claims.FindAsync(claim.Id);
            Assert.Equal("Approved", updated.Status);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ManagerDashboard", redirect.ActionName);
        }

        [Fact]
        public async Task Reject_AsCoordinator_SetsRejectedAndAddsNote()
        {
            _controller.HttpContext.Session.SetString("UserRole", "Coordinator");

            var claim = new Claim { Status = "Pending", LecturerId = "test@test.com" };
            _context.Claims.Add(claim);
            await _context.SaveChangesAsync();

            var result = await _controller.Reject(claim.Id, "Missing proof");

            var updated = await _context.Claims.FindAsync(claim.Id);
            Assert.Equal("Rejected", updated.Status);
            Assert.Equal("Missing proof", updated.CoordinatorNotes);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("CoordinatorDashboard", redirect.ActionName);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }

    // Minimal session implementation
    public class MockHttpSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id => "test-session";
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);

        public void SetString(string key, string value) => Set(key, System.Text.Encoding.UTF8.GetBytes(value));
        public string GetString(string key) => TryGetValue(key, out var v) ? System.Text.Encoding.UTF8.GetString(v) : null;
    }
}