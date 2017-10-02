﻿using System;
using System.Runtime.CompilerServices;

namespace Prime.Core
{
    public class ApiResponseException : Exception
    {
        public ApiResponseException(string message)
        {
            Message = message;
        }

        public ApiResponseException(string message, INetworkProvider provider, [CallerMemberName] string method = "Unknown")
        {
            Message = message + " - " + method + " in " + provider.Title + " provider.";
        }

        public override string Message { get; }
    }
}