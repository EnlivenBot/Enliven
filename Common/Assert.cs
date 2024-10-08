﻿using System;
using NLog;

namespace Common;

public static class Assert
{
    private static Logger _logger = LogManager.GetCurrentClassLogger();

    public static void NotNull(object? o)
    {
        if (o != null) return;
        var exception = new NullReferenceException();
        _logger.Error(exception, "Object is null in assert");
        //throw exception;
    }
}