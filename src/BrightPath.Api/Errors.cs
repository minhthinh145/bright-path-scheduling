namespace BrightPath.Api;

// A business rule refused the request -> 409 { code, rule, message }.
public class RuleException : Exception
{
    public string Code { get; }
    public string Rule { get; }
    public RuleException(string code, string rule, string message) : base(message)
    { Code = code; Rule = rule; }
}

// The input itself is malformed -> 422 { message }.
public class BadInputException : Exception
{
    public BadInputException(string message) : base(message) { }
}

// The id asked for does not exist -> 404.
public class NotFoundException : Exception { }
