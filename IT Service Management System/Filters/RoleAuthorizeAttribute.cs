namespace IT_Service_Management_System.Filters
{
    /// <summary>
    /// Marks a controller or action as restricted to the given roles. Enforced by
    /// <see cref="RoleAuthorizationFilter"/>. An action-level attribute overrides a
    /// controller-level one. Use <see cref="AllowAnyRoleAttribute"/> to exempt a
    /// specific action from a controller-level restriction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RoleAuthorizeAttribute : Attribute
    {
        public string[] Roles { get; }

        public RoleAuthorizeAttribute(params string[] roles) => Roles = roles;
    }

    /// <summary>Exempts an action from any controller-level <see cref="RoleAuthorizeAttribute"/>
    /// (any authenticated user may access it).</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AllowAnyRoleAttribute : Attribute { }
}
