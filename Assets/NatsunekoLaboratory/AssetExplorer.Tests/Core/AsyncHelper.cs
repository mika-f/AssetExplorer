// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace NatsunekoLaboratory.AssetExplorer.Tests
{
    /// <summary>
    ///     Unity runs EditMode tests on the main thread under a synchronization context; blocking there on code that resumes
    ///     on that context would deadlock. Running the work on the thread pool avoids it. Exceptions are rethrown unwrapped.
    /// </summary>
    internal static class AsyncHelper
    {
        public static T Run<T>(Func<Task<T>> work)
        {
            return Task.Run(work).GetAwaiter().GetResult();
        }

        public static void Run(Func<Task> work)
        {
            Task.Run(work).GetAwaiter().GetResult();
        }
    }
}
