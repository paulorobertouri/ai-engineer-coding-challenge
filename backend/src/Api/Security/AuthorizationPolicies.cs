namespace Api.Security;

public static class AuthorizationPolicies
{
    public const string ChatUser = nameof(ChatUser);
    public const string KnowledgeAdmin = nameof(KnowledgeAdmin);
    public const string Operator = nameof(Operator);
}
