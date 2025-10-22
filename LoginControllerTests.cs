using Moq;
using PROG6212_POE.Controllers;
using PROG6212_POE.Models;
using PROG6212_POE.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Xunit;

namespace PROG6212_POE.Tests
{
    public class LoginControllerTests
    {
        private readonly Mock<TableService> _mockTableService = new();
        private readonly LoginController _controller;

        public LoginControllerTests()
        {
            _controller = new LoginController(_mockTableService.Object);
        }

        [Fact]
        public async Task Index_ValidLogin_Redirects()
        {
            var user = new User { Email = "test@email.com", Password = "Test123!", Role = "Lecturer" };
            _mockTableService.Setup(t => t.GetUserAsync("test@email.com")).ReturnsAsync(user);

            var result = await _controller.Index("test@email.com", "pass");

            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task Index_InvalidLogin_ShowsError()
        {
            _mockTableService.Setup(t => t.GetUserAsync("test@email.com")).ReturnsAsync((User)null);

            var result = await _controller.Index("test@email.com", "wrong");

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(viewResult.ViewData["Error"]);
        }
    }
}
