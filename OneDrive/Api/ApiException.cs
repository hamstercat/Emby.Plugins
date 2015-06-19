using System;

namespace OneDrive.Api
{
    public class ApiException : Exception
    {
        public ApiException(string message)
            : base(message)
        { }
    }
}
