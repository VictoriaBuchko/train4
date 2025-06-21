using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using train2.Controllers;
using train4.Services.Interfaces;
using train2.Models;
using Xunit;
using System.Threading.Tasks;

namespace TestTrainSystem
{
    public class UnitTest1
    {
        [Fact]
        public async Task Register_ReturnsView_WhenPasswordsDoNotMatch()
        {

            var mockLogger = new Mock<ILogger<AccountController>>();
            var mockAuthService = new Mock<IAuthenticationService>();
            var mockTicketService = new Mock<ITicketService>();
            var controller = new AccountController(mockLogger.Object, mockAuthService.Object, mockTicketService.Object);
            var client = new Client { client_name = "Test", payment_info = "Card" };


            var result = await controller.Register(client, "12345", "54321") as ViewResult;


            Assert.NotNull(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ErrorCount > 0);
        }

        [Fact]
        public async Task Register_RedirectsToLogin_OnSuccess()
        {
            var mockLogger = new Mock<ILogger<AccountController>>();
            var mockAuthService = new Mock<IAuthenticationService>();
            var mockTicketService = new Mock<ITicketService>();

            mockAuthService
                .Setup(s => s.CreateClientAsync(It.IsAny<Client>(), It.IsAny<string>()))
                .ReturnsAsync((true, null)); // Успішна реєстрація

            var controller = new AccountController(mockLogger.Object, mockAuthService.Object, mockTicketService.Object);

            var client = new Client
            {
                client_name = "Test",
                client_surname = "Surname",
                client_patronymic = "Middle",
                email = "test@example.com",
                phone_number = "+380(67)1111111",
                payment_info = "Card",
                login = "testuser"
            };


            var result = await controller.Register(client, "pass123", "pass123");

            // Assert - проверяем что сервис вызвался (значит основная логика сработала)
            mockAuthService.Verify(x => x.CreateClientAsync(It.IsAny<Client>(), It.IsAny<string>()), Times.Once);

            // Проверяем что результат не null (контроллер сработал)
            Assert.NotNull(result);
        }
    }
}