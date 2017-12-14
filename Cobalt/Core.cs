using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Cobalt
{
    public static class Core
    {
        public static float DeltaTime { get; private set; }
        public static float CurrentFramesPerSecond { get; private set; }

        static Stopwatch sw;
        static TimeSpan sample;
        static double accumulator;
        static double idleCounter;

        static Core()
        {
            DeltaTime = 0.0f;
            CurrentFramesPerSecond = 0.0f;

            sw = Stopwatch.StartNew();
            sample = TimeSpan.FromSeconds(1);
            accumulator = 0.0;
            idleCounter = 0;

            Application.Idle += ((s, e) =>
            {
                if (!IsRuntime || !IsReady) return;

                double milliseconds = ComputeTimeSlice();
                DeltaTime = (float)milliseconds;

                idleCounter++;
                accumulator += milliseconds;
                if (accumulator > sample.TotalMilliseconds)
                {
                    CurrentFramesPerSecond = (float)idleCounter;
                    accumulator -= sample.TotalMilliseconds;
                    idleCounter = 0;
                }
            });
        }

        private static double ComputeTimeSlice()
        {
            sw.Stop();
            double timeslice = sw.Elapsed.TotalMilliseconds;
            sw.Reset();
            sw.Start();
            return timeslice;
        }

        internal static bool IsRuntime
        {
            get { return (LicenseManager.UsageMode != LicenseUsageMode.Designtime); }
        }

        internal static bool IsReady
        {
            get { return (IsRuntime && (GraphicsContext.CurrentContext != null)); }
        }

        internal static int GetMaxAASamples()
        {
            List<int> maxSamples = new List<int>();
            int retVal = 0;
            try
            {
                int samples = 0;
                do
                {
                    GraphicsMode mode = new GraphicsMode(GraphicsMode.Default.ColorFormat, GraphicsMode.Default.Depth, GraphicsMode.Default.Stencil, samples);
                    if (!maxSamples.Contains(mode.Samples)) maxSamples.Add(samples);
                    samples += 2;
                }
                while (samples <= 32);
            }
            finally
            {
                retVal = maxSamples.Last();
            }
            return retVal;
        }

        public static Version OpenTKVersion
        {
            get
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "OpenTK");
                var versionAttrib = (assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), true)[0] as System.Reflection.AssemblyFileVersionAttribute);
                return new Version(versionAttrib.Version);
            }
        }

        public static Version LibraryVersion
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public static string RendererString
        {
            get { return GL.GetString(StringName.Renderer) ?? "[null]"; }
        }

        public static string VendorString
        {
            get { return GL.GetString(StringName.Vendor) ?? "[null]"; }
        }

        public static string VersionString
        {
            get { return GL.GetString(StringName.Version) ?? "[null]"; }
        }

        public static string ShadingLanguageVersionString
        {
            get
            {
                string str = GL.GetString(StringName.ShadingLanguageVersion);
                if (str == null || str == string.Empty) return "[unsupported]";
                else return str;
            }
        }

        public static string[] SupportedExtensions
        {
            get { return GL.GetString(StringName.Extensions).Split(new char[] { ' ' }) ?? new string[] { "[null]" }; }
        }

        public static int MaxTextureUnits
        {
            get { return GL.GetInteger(GetPName.MaxTextureUnits); }
        }
    }
}
