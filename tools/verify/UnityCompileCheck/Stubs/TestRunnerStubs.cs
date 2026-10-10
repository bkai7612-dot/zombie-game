// Minimal stand-in for UnityEngine.TestRunner's attribute (compile check of PlayMode tests only).
namespace UnityEngine.TestTools
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class UnityTestAttribute : System.Attribute { }
}
