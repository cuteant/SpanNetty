// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv.Native
{
    using DotNetty.Common.Internal.Logging;
    using System;
    using System.Runtime.InteropServices;

    sealed unsafe class Loop
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Loop>();
        static readonly uv_walk_cb WalkCallback = OnWalkCallback;
        IntPtr handle;

        public Loop()
        {
            int size = NativeMethods.uv_loop_size().ToInt32();
            this.handle = Marshal.AllocHGlobal(size);
            try
            {
                int result = NativeMethods.uv_loop_init(this.handle);
                if (result < 0)
                {
                    throw NativeMethods.CreateError((uv_err_code)result);
                }
            }
            catch
            {
                Marshal.FreeHGlobal(this.handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_loop_t*)this.handle)->data = GCHandle.ToIntPtr(gcHandle);

            if (Logger.InfoEnabled)
            {
                Logger.Info($"Loop {this.handle} allocated.");
            }
        }

        public IntPtr Handle => this.handle;

        public bool IsAlive => this.handle != IntPtr.Zero && NativeMethods.uv_loop_alive(this.handle) != 0;

        public long Now
        {
            get
            {
                this.Validate();
                return NativeMethods.uv_now(this.handle);
            }
        }

        public long NowInHighResolution
        {
            get
            {
                this.Validate();
                return NativeMethods.uv_hrtime(this.handle);
            }
        }

        public int GetBackendTimeout()
        {
            return NativeMethods.uv_backend_timeout(this.handle); 
        }

        public int ActiveHandleCount() => 
            this.handle != IntPtr.Zero
            ? (int)((uv_loop_t*)this.handle)->active_handles 
            : 0;

        public int Run(uv_run_mode mode)
        {
            this.Validate();
            return NativeMethods.uv_run(this.handle, mode);
        }

        public void Stop()
        {
            this.Validate();
            NativeMethods.uv_stop(this.handle);
        }

        void Validate()
        {
            if (this.handle != IntPtr.Zero)
            {
                return;
            }

            throw new ObjectDisposedException($"{this.GetType().Name} has already been disposed");
        }

        public void Close()
        {
            IntPtr loopHandle = this.handle;
            if (loopHandle == IntPtr.Zero)
            {
                return;
            }

            // Get gc handle before close loop
            IntPtr pHandle = ((uv_loop_t*)loopHandle)->data;

            // Close loop
            try
            {
                int retry = 0;
                while (retry < 10)
                {
                    // Force close all active handles before close the loop
                    NativeMethods.uv_walk(loopHandle, WalkCallback, loopHandle);
                    Logger.Debug($"Loop {loopHandle} walk all handles completed.");

                    // Loop.Run here actually blocks in some intensive situitions 
                    // and it is highly unpredictable. For now, we rely on the users 
                    // to do the right things before disposing the loop, 
                    // e.g. close all handles before calling this.
                    // NativeMethods.RunLoop(handle, uv_run_mode.UV_RUN_DEFAULT);
                    int result = NativeMethods.uv_loop_close(loopHandle);
                    if (result >= 0)
                    {
                        break;
                    }
                    else
                    {
                        OperationException error = NativeMethods.CreateError((uv_err_code)result);
                        // Only retry if loop close return busy
                        if (error.Name != "EBUSY")
                        {
                            throw error;
                        }
                    }

                    retry++;
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Loop {loopHandle} error attempt to run loop once before closing. {exception}");
            }

            Logger.Info($"Loop {loopHandle} closed.");

            // Free GCHandle
            if (pHandle != IntPtr.Zero)
            {
                GCHandle nativeHandle = GCHandle.FromIntPtr(pHandle);
                if (nativeHandle.IsAllocated)
                {
                    nativeHandle.Free();
                    ((uv_loop_t*)loopHandle)->data = IntPtr.Zero;
                    Logger.Info($"Loop {loopHandle} GCHandle released.");
                }
            }

            // Release memory
            Marshal.FreeHGlobal(loopHandle);
            this.handle = IntPtr.Zero;
            Logger.Info($"Loop {loopHandle} memory released.");
        }

        static void OnWalkCallback(IntPtr handle, IntPtr loopHandle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // All handles must implement IDisposable
                var target = NativeHandle.GetTarget<IDisposable>(handle);
                target?.Dispose();
                Logger.Info($"Loop {loopHandle} walk callback disposed {handle} {target?.GetType()}");
            }
            catch (Exception exception)
            {
                Logger.Warn($"Loop {loopHandle} Walk callback attempt to close handle {handle} failed. {exception}");
            }
        }
    }
}
