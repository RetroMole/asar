using Serilog;
using System.Runtime.InteropServices;

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CA1401 // P/Invokes should not be visible
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8601 // Possible null reference assignment.

public static unsafe partial class Asar
{
    public const string DllPath = "libasar";
    public const int ExpectedApiVersion = 303;

    public static string Ver2Str(int ver)
    {
        //major*10000+minor*100+bugfix*1.
        //123456 = 12.3456
        int maj = ver / 10000;
        int min = (ver - (maj * 10000)) / 100;
        int fx = (ver - ((maj * 10000) + (min * 100))) / 1;
        return $"{maj}.{min}{fx}";
    }

    [DllImport(DllPath, EntryPoint = "asar_init")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AsarInit();

    [DllImport(DllPath, EntryPoint = "asar_close")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Close();

    [DllImport(DllPath, EntryPoint = "asar_version")]
    public static extern int Version();

    [DllImport(DllPath, EntryPoint = "asar_apiversion")]
    public static extern int ApiVersion();

    [DllImport(DllPath, EntryPoint = "asar_reset")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Reset();

    [DllImport(DllPath, EntryPoint = "asar_patch", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool _Patch(string patchLocation, byte* romData, int bufLen, int* romLength);

    [DllImport(DllPath, EntryPoint = "asar_patch_ex")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool _Patch_ex(ref RawPatchParams parameters);

    [DllImport(DllPath, EntryPoint = "asar_maxromsize")]
    public static extern int MaxROMSize();

    [DllImport(DllPath, EntryPoint = "asar_geterrors")]
    private static extern RawAsarError* _GetErrors(out int length);

    [DllImport(DllPath, EntryPoint = "asar_getwarnings")]
    private static extern RawAsarError* _GetWarnings(out int length);

    [DllImport(DllPath, EntryPoint = "asar_getprints")]
    private static extern void** _GetPrints(out int length);

    [DllImport(DllPath, EntryPoint = "asar_getalllabels")]
    private static extern RawAsarLabel* _GetAllLabels(out int length);

    [DllImport(DllPath, EntryPoint = "asar_getlabelval", CharSet = CharSet.Unicode)]
    private static extern int GetLabelVal(string labelName);

    [DllImport(DllPath, EntryPoint = "asar_getdefine", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.AnsiBStr)]
    public static extern string GetDefine(string defineName);

    [DllImport(DllPath, EntryPoint = "asar_getalldefines")]
    private static extern RawAsarDefine* _GetAllDefines(out int length);

    [DllImport(DllPath, EntryPoint = "asar_resolvedefines", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.AnsiBStr)]
    public static extern string ResolveDefines(string data, bool learnNew);

    [DllImport(DllPath, EntryPoint = "asar_math", CharSet = CharSet.Unicode)]
    private static extern double AsarMath(string math, out IntPtr error);

    [DllImport(DllPath, EntryPoint = "asar_getwrittenblocks")]
    private static extern RawAsarWrittenBlock* _GetWrittenBlocks(out int length);

    [DllImport(DllPath, EntryPoint = "asar_getmapper")]
    public static extern MapperType GetMapper();

    [DllImport(DllPath, EntryPoint = "asar_getsymbolsfile", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.AnsiBStr)]
    public static extern string GetSymbolsFile(string format = "wla");

    public static bool Init()
    {
        try
        {
            if (ApiVersion() < ExpectedApiVersion || (ApiVersion() / 100) > (ExpectedApiVersion / 100))
            {
                Log.Error("[ASAR] Expected ApiVersion mismatch!");
                return false;
            }
            return AsarInit();
        }
        catch { return false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawPatchParams
    {
        public int structsize;
        public string patchloc;
        public byte* romdata;
        public int buflen;
        public int* romlen;
        public byte** includepaths;
        public int numincludepaths;
        [MarshalAs(UnmanagedType.I1)]
        public bool should_reset;
        public RawAsarDefine* additional_defines;
        public int additional_define_count;
        public string stdincludesfile;
        public string stddefinesfile;
    };

    /// <summary>
    /// Applies a patch.
    /// </summary>
    /// <param name="patchLocation">The patch location.</param>
    /// <param name="romData">The rom data. It must not be headered.</param>
    /// <param name="includePaths">lists additional include paths</param>
    /// <param name="shouldReset">specifies whether asar should clear out all defines, labels, etc from the last inserted file.<br/> 
    /// Setting it to False will make Asar act like the currently patched file was directly appended to the previous one.</param>
    /// <param name="additionalDefines">specifies extra defines to give to the patch</param>
    /// <param name="stdIncludeFile">path to a file that specifes additional include paths</param>
    /// <param name="stdDefineFile">path to a file that specifes additional defines</param>
    /// <returns>True if no errors.</returns>
    public static bool Patch(string patchLocation, ref byte[] romData, string[] includePaths = null,
        bool shouldReset = true, Dictionary<string, string> additionalDefines = null,
        string stdIncludeFile = null, string stdDefineFile = null)
    {
        if (includePaths == null)
        {
            includePaths = Array.Empty<string>();
        }
        if (additionalDefines == null)
        {
            additionalDefines = new Dictionary<string, string>();
        }

        var includes = new byte*[includePaths.Length];
        var defines = new RawAsarDefine[additionalDefines.Count];

        try
        {
            for (int i = 0; i < includePaths.Length; i++)
            {
                includes[i] = (byte*)Marshal.StringToCoTaskMemAnsi(includePaths[i]);
            }

            var keys = additionalDefines.Keys.ToArray();

            for (int i = 0; i < additionalDefines.Count; i++)
            {
                var name = keys[i];
                var value = additionalDefines[name];
                defines[i].name = Marshal.StringToCoTaskMemAnsi(name);
                defines[i].contents = Marshal.StringToCoTaskMemAnsi(value);
            }

            int newsize = MaxROMSize();
            int length = romData.Length;

            if (length < newsize)
            {
                Array.Resize(ref romData, newsize);
            }

            bool success;

            fixed (byte* ptr = romData)
            fixed (byte** includepaths = includes)
            fixed (RawAsarDefine* additionalDefines2 = defines)
            {
                var param = new RawPatchParams
                {
                    patchloc = patchLocation,
                    romdata = ptr,
                    buflen = newsize,
                    romlen = &length,

                    should_reset = shouldReset,
                    includepaths = includepaths,
                    numincludepaths = includes.Length,
                    additional_defines = additionalDefines2,
                    additional_define_count = defines.Length,
                    stddefinesfile = stdDefineFile,
                    stdincludesfile = stdIncludeFile
                };
                param.structsize = Marshal.SizeOf(param);

                success = _Patch_ex(ref param);
            }

            if (length < newsize)
            {
                Array.Resize(ref romData, length);
            }

            return success;
        }
        finally
        {
            for (int i = 0; i < includes.Length; i++)
            {
                Marshal.FreeCoTaskMem((IntPtr)includes[i]);
            }

            foreach (var define in defines)
            {
                Marshal.FreeCoTaskMem(define.name);
                Marshal.FreeCoTaskMem(define.contents);
            }

            if (GetPrints().Length > 0)
            {
                Log.Information("[ASAR] Prints:");
                foreach (string p in GetPrints())
                {
                    Log.Information("[ASAR]: {0}", p);
                }
            }

            if (GetWarnings().Length > 0)
            {
                Log.Information("[ASAR] Warnings:");
                foreach (Error w in GetWarnings())
                {
                    Log.Warning("[ASAR]: {0}", w.Fullerrdata);
                }
            }

            if (GetErrors().Length > 0)
            {
                Log.Information("[ASAR] Errors:");
                foreach (Error e in GetErrors())
                {
                    Log.Error("[ASAR]: {0}", e.Fullerrdata);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct RawAsarError
    {
        public readonly IntPtr fullerrdata;
        public readonly IntPtr rawerrdata;
        public readonly IntPtr block;
        public readonly IntPtr filename;
        public readonly int line;
        public readonly IntPtr callerfilename;
        public readonly int callerline;
        public readonly int errid;
    };

    private static Error[] CleanErrors(RawAsarError* ptr, int length)
    {
        Error[] output = new Error[length];

        // Copy unmanaged to managed memory to avoid potential errors in case the area
        // gets cleared by Asar.
        for (int i = 0; i < length; i++)
        {
            output[i].Fullerrdata = Marshal.PtrToStringAnsi(ptr[i].fullerrdata);
            output[i].Rawerrdata = Marshal.PtrToStringAnsi(ptr[i].rawerrdata);
            output[i].Block = Marshal.PtrToStringAnsi(ptr[i].block);
            output[i].Filename = Marshal.PtrToStringAnsi(ptr[i].filename);
            output[i].Line = ptr[i].line;
            output[i].Callerfilename = Marshal.PtrToStringAnsi(ptr[i].callerfilename);
            output[i].Callerline = ptr[i].callerline;
            output[i].ErrorId = ptr[i].errid;
        }

        return output;
    }

    public static Error[] GetErrors()
    {
        RawAsarError* ptr = _GetErrors(out int length);
        return CleanErrors(ptr, length);
    }

    public static Error[] GetWarnings()
    {
        RawAsarError* ptr = _GetWarnings(out int length);
        return CleanErrors(ptr, length);
    }

    public static string[] GetPrints()
    {
        void** ptr = _GetPrints(out int length);

        string[] output = new string[length];

        for (int i = 0; i < length; i++)
        {
            output[i] = Marshal.PtrToStringAnsi((IntPtr)ptr[i]);
        }

        return output;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawAsarLabel
    {
        public readonly IntPtr name;
        public readonly int location;
    }

    public static Label[] GetAllLabels()
    {
        RawAsarLabel* ptr = _GetAllLabels(out int length);
        Label[] output = new Label[length];

        // Copy unmanaged to managed memory to avoid potential errors in case the area
        // gets cleared by Asar.
        for (int i = 0; i < length; i++)
        {
            output[i].Name = Marshal.PtrToStringAnsi(ptr[i].name);
            output[i].Location = ptr[i].location;
        }

        return output;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawAsarDefine
    {
        public IntPtr name;
        public IntPtr contents;
    }

    public static Define[] GetAllDefines()
    {
        RawAsarDefine* ptr = _GetAllDefines(out int length);
        Define[] output = new Define[length];

        // Copy unmanaged to managed memory to avoid potential errors in case the area
        // gets cleared by Asar.
        for (int i = 0; i < length; i++)
        {
            output[i].Name = Marshal.PtrToStringAnsi(ptr[i].name);
            output[i].Contents = Marshal.PtrToStringAnsi(ptr[i].contents);
        }

        return output;
    }

    public static double Math(string math, out string error)
    {
        double value = AsarMath(math, out IntPtr err);

        error = Marshal.PtrToStringAnsi(err);
        return value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawAsarWrittenBlock
    {
        public readonly int pcoffset;
        public readonly int snesoffset;
        public readonly int numbytes;
    };

    private static WrittenBlock[] CleanWrittenBlocks(RawAsarWrittenBlock* ptr, int length)
    {
        WrittenBlock[] output = new WrittenBlock[length];

        // Copy unmanaged to managed memory to avoid potential errors in case the area
        // gets cleared by Asar.
        for (int i = 0; i < length; i++)
        {
            output[i].Snesoffset = ptr[i].snesoffset;
            output[i].Numbytes = ptr[i].numbytes;
            output[i].Pcoffset = ptr[i].pcoffset;
        }

        return output;
    }

    public static WrittenBlock[] GetWrittenBlocks()
    {
        RawAsarWrittenBlock* ptr = _GetWrittenBlocks(out int length);
        return CleanWrittenBlocks(ptr, length);
    }

    public struct Error
    {
        public string Fullerrdata;
        public string Rawerrdata;
        public string Block;
        public string Filename;
        public int Line;
        public string Callerfilename;
        public int Callerline;
        public int ErrorId;
    }

    public struct Label
    {
        public string Name;
        public int Location;
    }

    public struct Define
    {
        public string Name;
        public string Contents;
    }

    public struct WrittenBlock
    {
        public int Pcoffset;
        public int Snesoffset;
        public int Numbytes;
    }

    public enum MapperType
    {
        InvalidMapper,
        LoRom,
        HiRom,
        Sa1Rom,
        BigSa1Rom,
        SfxRom,
        ExLoRom,
        ExHiRom,
        NoRom
    }
}

#pragma warning restore CA1401 // P/Invokes should not be visible
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8601 // Possible null reference assignment.
