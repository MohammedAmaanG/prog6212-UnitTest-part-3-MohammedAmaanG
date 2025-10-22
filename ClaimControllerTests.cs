using Moq;
using PROG6212_POE.Controllers;
using PROG6212_POE.Models;
using PROG6212_POE.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PROG6212_POE.Tests
{
    public class ClaimControllerTests
    {
        private readonly Mock<TableService> _mockTableService = new();
        private readonly Mock<BlobService> _mockBlobService = new();
        private readonly ClaimController _controller;

        public ClaimControllerTests()
        {
            _controller = new ClaimController(_mockTableService.Object, _mockBlobService.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new Mock<ISession>().Object;
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        [Fact]
        public async Task Submit_ValidClaim_SavesAndRedirects()
        {
            var model = new Claim { HoursWorked = 10, HourlyRate = 100, Notes = "Test" };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());
            fileMock.Setup(f => f.FileName).Returns("test.pdf");
            _mockBlobService.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("url");
            _mockTableService.Setup(t => t.AddClaimAsync(It.IsAny<Claim>())).Returns(Task.CompletedTask);
            _mockTableService.Setup(t => t.AddDocumentAsync(It.IsAny<Document>())).Returns(Task.CompletedTask);
            _controller.HttpContext.Session.SetString("UserRole", "Lecturer");
            _controller.HttpContext.Session.SetString("UserEmail", "test@email.com");

            var result = await _controller.Submit(model, fileMock.Object);

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Submit_Unauthorized_ReturnsUnauthorized()
        {
            var model = new Claim();
            _controller.HttpContext.Session.SetString("UserRole", "Invalid");

            var result = _controller.Submit();

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Approve_ValidClaim_UpdatesStatus()
        {
            var claim = new Claim { RowKey = "1", HoursWorked = 50, HourlyRate = 100, Status = "Pending" };
            _mockTableService.Setup(t => t.GetClaimAsync("1")).ReturnsAsync(claim);
            _mockTableService.Setup(t => t.UpdateClaimAsync(claim)).Returns(Task.CompletedTask);
            _controller.HttpContext.Session.SetString("UserRole", "Coordinator");

            var result = await _controller.Approve("1");

            Assert.Equal("Verified", claim.Status);
            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Approve_InvalidId_ReturnsNotFound()
        {
            _mockTableService.Setup(t => t.GetClaimAsync("invalid")).ReturnsAsync((Claim)null);
            _controller.HttpContext.Session.SetString("UserRole", "Coordinator");

            var result = await _controller.Approve("invalid");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Reject_ValidClaim_UpdatesStatus()
        {
            var claim = new Claim { RowKey = "1", Status = "Pending" };
            _mockTableService.Setup(t => t.GetClaimAsync("1")).ReturnsAsync(claim);
            _mockTableService.Setup(t => t.UpdateClaimAsync(claim)).Returns(Task.CompletedTask);
            _controller.HttpContext.Session.SetString("UserRole", "Manager");

            var result = await _controller.Reject("1");

            Assert.Equal("Rejected", claim.Status);
            Assert.IsType<RedirectToActionResult>(result);
        }
    }
}
