using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Controllers;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LiMS.Tests;

public sealed class ControllerPrincipalTests
{
    [Fact]
    public async Task ReaderController_uses_authenticated_account_for_notification_queries()
    {
        const int accountId = 93;
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(item => item.GetUnreadCountAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), notificationService.Object, accountId);

        var result = await controller.GetUnreadNotificationCount(CancellationToken.None);

        Assert.Equal(4, Assert.IsType<OkObjectResult>(result.Result).Value);
        notificationService.Verify(item => item.GetUnreadCountAsync(accountId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReaderController_returns_notifications_for_current_account()
    {
        const int accountId = 41;
        IReadOnlyList<ReaderNotificationResponse> expected =
        [new ReaderNotificationResponse(3, "ReservationReady", "Your book is ready", "ReservationReady", DateTimeOffset.UtcNow, false)];
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(item => item.GetNotificationsAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), notificationService.Object, accountId);

        var result = await controller.GetNotifications(CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result.Result).Value);
        notificationService.Verify(item => item.GetNotificationsAsync(accountId, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task ReaderController_returns_expected_status_when_marking_notification(bool succeeded, int statusCode)
    {
        const int accountId = 12;
        const long notificationId = 44;
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(item => item.MarkAsReadAsync(
                accountId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeded);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), notificationService.Object, accountId);

        var result = await controller.MarkNotificationAsRead(notificationId, CancellationToken.None);

        Assert.Equal(statusCode, ((StatusCodeResult)result).StatusCode);
        notificationService.Verify(item => item.MarkAsReadAsync(accountId, notificationId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReaderController_marks_all_notifications_for_current_account()
    {
        const int accountId = 6;
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(item => item.MarkAllAsReadAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), notificationService.Object, accountId);

        var result = await controller.MarkAllNotificationsAsRead(CancellationToken.None);

        Assert.Equal(8, Assert.IsType<OkObjectResult>(result.Result).Value);
        notificationService.Verify(item => item.MarkAllAsReadAsync(accountId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReaderController_returns_not_found_when_notification_does_not_belong_to_current_account()
    {
        var service = new Mock<INotificationService>();
        service.Setup(item => item.MarkAsReadAsync(8, 55, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), service.Object, 8);

        var result = await controller.MarkNotificationAsRead(55, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReaderController_throws_unauthorized_when_claim_is_missing()
    {
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var controller = CreateReaderController(Mock.Of<IReaderService>(), notificationService.Object, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.GetUnreadNotificationCount(CancellationToken.None));
        notificationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReaderController_passes_current_account_to_reservation_creation()
    {
        const int accountId = 70;
        var expected = new ReservationResponse(5, 9, "Book title", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2), "Pending");
        var readerService = new Mock<IReaderService>();
        readerService.Setup(item => item.CreateReservationAsync(
                accountId, It.IsAny<CreateReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateReaderController(readerService.Object, Mock.Of<INotificationService>(), accountId);

        var result = await controller.CreateReservation(
            new CreateReservationRequest { BookId = 9 }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ReaderController.GetReservation), created.ActionName);
        Assert.Equal(expected, created.Value);
        readerService.Verify(item => item.CreateReservationAsync(
            accountId,
            It.Is<CreateReservationRequest>(request => request.BookId == 9),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AdminController_passes_actor_id_to_book_creation()
    {
        const int actorId = 17;
        var expected = new BookResponse(2, "Book", "Author", null, "Category", null, null, 1, 1, 0, null, null, null);
        var adminService = new Mock<IAdminService>();
        adminService.Setup(item => item.CreateBookAsync(
                It.IsAny<CreateBookRequest>(), actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateAdminController(adminService.Object, Mock.Of<IDatabaseMaintenanceService>(), actorId);

        var result = await controller.CreateBook(new CreateBookRequest
        {
            Category = "Category",
            Title = "Book",
            Author = "Author",
            InitialCopies = 1
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AdminController.GetBook), created.ActionName);
        Assert.Equal(expected, created.Value);
        adminService.Verify(item => item.CreateBookAsync(
            It.Is<CreateBookRequest>(request => request.Title == "Book"),
            actorId,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AdminController_passes_actor_id_to_fine_collection()
    {
        const int actorId = 18;
        const long fineId = 21;
        var expected = new FineResponse(fineId, 4, "Book", "Reader", "CARD", 100m, "Fine", "Paid", DateTimeOffset.UtcNow);
        var adminService = new Mock<IAdminService>();
        adminService.Setup(item => item.CollectFineAsync(
                fineId, It.IsAny<CollectFineRequest>(), actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateAdminController(adminService.Object, Mock.Of<IDatabaseMaintenanceService>(), actorId);

        var result = await controller.CollectFine(
            fineId,
            new CollectFineRequest { Amount = 100m, Reason = "Cash" },
            CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result.Result).Value);
        adminService.Verify(item => item.CollectFineAsync(
            fineId,
            It.Is<CollectFineRequest>(request => request.Amount == 100m),
            actorId,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AdminController_requires_actor_claim_for_sensitive_backup_creation()
    {
        var backupService = new Mock<IDatabaseMaintenanceService>(MockBehavior.Strict);
        var controller = CreateAdminController(Mock.Of<IAdminService>(), backupService.Object, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.CreateDatabaseBackup(CancellationToken.None));
        backupService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AdminController_passes_actor_id_to_restore_operation()
    {
        const int actorId = 22;
        const string fileName = "LiMSDB_backup_test.bak";
        var expected = new DatabaseRestoreResponse(
            "LiMSDB", fileName, "safety.bak", DateTimeOffset.UtcNow);
        var backupService = new Mock<IDatabaseMaintenanceService>();
        backupService.Setup(item => item.RestoreBackupAsync(
                fileName, "LiMSDB", actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateAdminController(Mock.Of<IAdminService>(), backupService.Object, actorId);

        var result = await controller.RestoreDatabaseBackup(
            fileName,
            new RestoreDatabaseBackupRequest { ConfirmDatabaseName = "LiMSDB" },
            CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result.Result).Value);
        backupService.Verify(item => item.RestoreBackupAsync(
            fileName, "LiMSDB", actorId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AdminController_uses_query_filters_without_changing_contract()
    {
        const string search = "history";
        const string status = "Active";
        var adminService = new Mock<IAdminService>();
        adminService.Setup(item => item.GetBooksAsync(search, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BookResponse>());
        adminService.Setup(item => item.GetReadersAsync(search, status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReaderResponse>());
        var controller = CreateAdminController(adminService.Object, Mock.Of<IDatabaseMaintenanceService>(), 1);

        await controller.Books(search, CancellationToken.None);
        await controller.Readers(search, status, CancellationToken.None);

        adminService.Verify(item => item.GetBooksAsync(search, CancellationToken.None), Times.Once);
        adminService.Verify(item => item.GetReadersAsync(search, status, CancellationToken.None), Times.Once);
    }

    private static ReaderController CreateReaderController(
        IReaderService readerService,
        INotificationService notificationService,
        int? accountId)
    {
        var controller = new ReaderController(readerService, notificationService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContext(accountId)
        };
        return controller;
    }

    private static AdminController CreateAdminController(
        IAdminService adminService,
        IDatabaseMaintenanceService databaseMaintenanceService,
        int? actorId)
    {
        var controller = new AdminController(adminService, databaseMaintenanceService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContext(actorId)
        };
        return controller;
    }

    private static HttpContext CreateHttpContext(int? accountId)
    {
        var claims = accountId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, accountId.Value.ToString()) }
            : [];
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "UnitTest"))
        };
    }
}
