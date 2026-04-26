using System;
using System.Threading.Tasks;
using Serilog;

namespace Bloom.Services;

internal static class TaskExtensions
{
    internal static async void FireAndForget(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unobserved exception in fire-and-forget task");
        }
    }
}
