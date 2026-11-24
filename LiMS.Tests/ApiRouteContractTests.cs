using System.Reflection;
using LiMS.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LiMS.Tests;

public sealed class ApiRouteContractTests
{
    public static IEnumerable<object[]> ExistingRoutes()
    {
        yield return Route(typeof(AuthController), nameof(AuthController.Login), "POST", "login");
        yield return Route(typeof(AuthController), nameof(AuthController.ChangePassword), "POST", "change-password");
        yield return Route(typeof(LibraryController), nameof(LibraryController.Health), "GET", "health");
        yield return Route(typeof(LibraryController), nameof(LibraryController.Books), "GET", "books");
        yield return Route(typeof(LibraryController), nameof(LibraryController.PagedBooks), "GET", "books/paged");
        yield return Route(typeof(LibraryController), nameof(LibraryController.BookCategories), "GET", "book-categories");
        yield return Route(typeof(LibraryController), nameof(LibraryController.GetPublicBook), "GET", "books/{id:int}");
        yield return Route(typeof(LibraryController), nameof(LibraryController.BookCover), "GET", "books/{id:int}/cover");
        yield return Route(typeof(LibraryController), nameof(LibraryController.GetUser), "GET", "users/{studentCode}");
        yield return Route(typeof(LibraryController), nameof(LibraryController.Loans), "GET", "loans/{studentCode}");
        yield return Route(typeof(LibraryController), nameof(LibraryController.Reservations), "GET", "reservations/{studentCode}");
        yield return Route(typeof(LibraryController), nameof(LibraryController.Notifications), "GET", "notifications/{studentCode}");
        yield return Route(typeof(ReaderController), nameof(ReaderController.UpdateProfile), "PUT", "profile");
        yield return Route(typeof(ReaderController), nameof(ReaderController.GetReservation), "GET", "reservation/{id:long}");
        yield return Route(typeof(ReaderController), nameof(ReaderController.CreateReservation), "POST", "reservations");
        yield return Route(typeof(ReaderController), nameof(ReaderController.CancelReservation), "DELETE", "reservations/{id:long}");
        yield return Route(typeof(ReaderController), nameof(ReaderController.Proposals), "GET", "proposals");
        yield return Route(typeof(ReaderController), nameof(ReaderController.GetProposal), "GET", "proposals/{id:int}");
        yield return Route(typeof(ReaderController), nameof(ReaderController.CreateProposal), "POST", "proposals");
        yield return Route(typeof(ReaderController), nameof(ReaderController.Renew), "POST", "loans/{id:long}/renew");
        yield return Route(typeof(ReaderController), nameof(ReaderController.GetNotifications), "GET", "notifications");
        yield return Route(typeof(ReaderController), nameof(ReaderController.GetUnreadNotificationCount), "GET", "notifications/unread-count");
        yield return Route(typeof(ReaderController), nameof(ReaderController.MarkNotificationAsRead), "PATCH", "notifications/{id:long}/read");
        yield return Route(typeof(ReaderController), nameof(ReaderController.MarkAllNotificationsAsRead), "PATCH", "notifications/read-all");
        yield return Route(typeof(AdminController), nameof(AdminController.Dashboard), "GET", "dashboard");
        yield return Route(typeof(AdminController), nameof(AdminController.Books), "GET", "books");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedBooks), "GET", "books/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.GetBook), "GET", "books/{id:int}");
        yield return Route(typeof(AdminController), nameof(AdminController.Copies), "GET", "books/{id:int}/copies");
        yield return Route(typeof(AdminController), nameof(AdminController.CreateBook), "POST", "books");
        yield return Route(typeof(AdminController), nameof(AdminController.UpdateBook), "PUT", "books/{id:int}");
        yield return Route(typeof(AdminController), nameof(AdminController.UploadBookCover), "POST", "books/{id:int}/cover");
        yield return Route(typeof(AdminController), nameof(AdminController.AddCopies), "POST", "books/{id:int}/copies");
        yield return Route(typeof(AdminController), nameof(AdminController.UpdateCopyStatus), "PUT", "copies/{id:int}/status");
        yield return Route(typeof(AdminController), nameof(AdminController.Readers), "GET", "readers");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedReaders), "GET", "readers/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.GetReader), "GET", "readers/{id:int}");
        yield return Route(typeof(AdminController), nameof(AdminController.CreateReader), "POST", "readers");
        yield return Route(typeof(AdminController), nameof(AdminController.UpdateReader), "PUT", "readers/{id:int}");
        yield return Route(typeof(AdminController), nameof(AdminController.ResetReaderPassword), "PUT", "readers/{id:int}/password");
        yield return Route(typeof(AdminController), nameof(AdminController.Loans), "GET", "loans");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedLoans), "GET", "loans/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.GetLoan), "GET", "loans/{id:long}");
        yield return Route(typeof(AdminController), nameof(AdminController.Checkout), "POST", "loans/checkout");
        yield return Route(typeof(AdminController), nameof(AdminController.Return), "POST", "loans/return");
        yield return Route(typeof(AdminController), nameof(AdminController.Renew), "POST", "loans/{id:long}/renew");
        yield return Route(typeof(AdminController), nameof(AdminController.Fines), "GET", "fines");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedFines), "GET", "fines/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.CollectFine), "PUT", "fines/{id:long}/collect");
        yield return Route(typeof(AdminController), nameof(AdminController.Proposals), "GET", "proposals");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedProposals), "GET", "proposals/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.ReviewProposal), "PUT", "proposals/{id:int}/review");
        yield return Route(typeof(AdminController), nameof(AdminController.Settings), "GET", "settings");
        yield return Route(typeof(AdminController), nameof(AdminController.UpdateSetting), "PUT", "settings/{key}");
        yield return Route(typeof(AdminController), nameof(AdminController.ActivityLogs), "GET", "activity-logs");
        yield return Route(typeof(AdminController), nameof(AdminController.PagedActivityLogs), "GET", "activity-logs/paged");
        yield return Route(typeof(AdminController), nameof(AdminController.DatabaseBackups), "GET", "database-backups");
        yield return Route(typeof(AdminController), nameof(AdminController.GetDatabaseBackup), "GET", "database-backups/{fileName}");
        yield return Route(typeof(AdminController), nameof(AdminController.CreateDatabaseBackup), "POST", "database-backups");
        yield return Route(typeof(AdminController), nameof(AdminController.RestoreDatabaseBackup), "POST", "database-backups/{fileName}/restore");
    }

    [Theory]
    [MemberData(nameof(ExistingRoutes))]
    public void Controller_action_keeps_expected_http_method_and_route_template(
        Type controllerType,
        string actionName,
        string httpMethod,
        string template)
    {
        var action = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(action);
        Assert.Contains(action!.GetCustomAttributes<HttpMethodAttribute>(), attribute =>
            attribute.Template == template
            && attribute.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase));
    }

    private static object[] Route(Type controller, string action, string httpMethod, string template) =>
        [controller, action, httpMethod, template];
}
