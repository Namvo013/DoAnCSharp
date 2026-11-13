namespace LiMS.Api.Services;

public sealed class BusinessRuleException(string message) : Exception(message)
{
}
