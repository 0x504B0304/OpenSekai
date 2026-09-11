using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sekai.MusicScoreMaker.Ingame.AudioAssist
{
    // Each job owns its session. Cancellation registration is disposed before the
    // native handle, so a late token callback cannot touch freed native memory.
    internal sealed class AudioAssistNative : IDisposable
    {
        private const string Library = "opensekai_audio";
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern int osa_version();
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr osa_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int threads);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr osa_error();
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void osa_close(IntPtr model);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern void osa_cancel(IntPtr model);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern int osa_separate(IntPtr model, float[] wave, [Out] float[] output);
        [DllImport(Library, CallingConvention=CallingConvention.Cdecl)] private static extern int osa_emissions(IntPtr model, float[] wave, int samples, [Out] float[] output, int capacity);
        private IntPtr handle;
        private CancellationToken token;
        private CancellationTokenRegistration cancellation;
        public static bool Available
        {
            get {try {return osa_version()==1;}catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}catch(BadImageFormatException){return false;}}
        }
        private static Exception Error() => new IOException("内置模型推理失败："+Marshal.PtrToStringUTF8(osa_error()));
        public AudioAssistNative(string path, int threads, CancellationToken token)
        {
            this.token=token;token.ThrowIfCancellationRequested();
            handle=osa_open(path,threads);
            if(handle==IntPtr.Zero)throw Error();
            cancellation=token.Register(()=>osa_cancel(handle));
        }
        public void Separate(float[] wave,float[] output)
        {
            token.ThrowIfCancellationRequested();
            if(wave.Length!=343980*2||output.Length!=343980*8)throw new ArgumentException("Invalid separator buffer");
            int result=osa_separate(handle,wave,output);token.ThrowIfCancellationRequested();
            if(result!=0)throw Error();
        }
        public float[] Emissions(float[] wave,out int frames)
        {
            token.ThrowIfCancellationRequested();
            var result=new float[(wave.Length/320+1)*28];
            frames=osa_emissions(handle,wave,wave.Length,result,result.Length);
            token.ThrowIfCancellationRequested();if(frames<1)throw Error();return result;
        }
        public void Dispose()
        {
            cancellation.Dispose();
            if(handle!=IntPtr.Zero){osa_close(handle);handle=IntPtr.Zero;}
        }
    }
}
