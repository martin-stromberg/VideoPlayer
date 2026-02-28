using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net;
using VideoWebPlayer.Services;

/// <summary>
/// Validates that requests originate from allowed internal connections.
/// </summary>
public class ConnectionCheckAttribute : ActionFilterAttribute
{
    
    /// <summary>
    /// Validates connection headers before the action executes.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var internalConnectionService = context.HttpContext.RequestServices.GetService<InternalConnectionService>();
        var isAllowed = context.HttpContext.Request.Headers.UserAgent == internalConnectionService.GetUserAgent();
        if (!isAllowed)
            isAllowed = internalConnectionService.IsAllowed(context.HttpContext.Connection.RemoteIpAddress);
        else
            internalConnectionService.Allow(context.HttpContext.Connection.RemoteIpAddress);
        if (!isAllowed)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ConnectionCheckAttribute>>();
            logger.LogWarning("Unauthorized access attempt detected. User-Agent: {UserAgent}; IP-Address: {Address} ", context.HttpContext.Response.Headers.UserAgent, context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedResult();
            return;
        }
        base.OnActionExecuting(context);
    }

    
}