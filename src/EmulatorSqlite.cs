using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PadLauncher {
    public static class EmulatorSqlite {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] name,out IntPtr db,int flags,IntPtr vfs);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int length,out IntPtr statement,IntPtr tail);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr statement);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr statement);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr statement,int column);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr statement,int column);
        public static List<string[]> ReadLibrary(string path,CancellationToken token) {
            if(!File.Exists(path)) throw new FileNotFoundException("The mGBA library database is missing.",path);
            IntPtr db=IntPtr.Zero,statement=IntPtr.Zero;
            try {
                if(sqlite3_open_v2(Encoding.UTF8.GetBytes(path+"\0"),out db,1,IntPtr.Zero)!=0) throw new IOException("Cannot open the mGBA library read-only. Close mGBA and retry.");
                byte[] query=Encoding.UTF8.GetBytes("SELECT roots.path, paths.path, COALESCE(NULLIF(paths.customTitle,''),NULLIF(roms.internalTitle,''),'') FROM paths JOIN roots USING(rootid) JOIN roms USING(romid);\0");
                if(sqlite3_prepare_v2(db,query,-1,out statement,IntPtr.Zero)!=0) throw new IOException("Unsupported or damaged mGBA library schema.");
                var rows=new List<string[]>(); int code;
                while((code=sqlite3_step(statement))==100) {
                    token.ThrowIfCancellationRequested(); var row=new string[3];
                    for(int col=0;col<3;col++) {IntPtr value=sqlite3_column_text(statement,col); int length=sqlite3_column_bytes(statement,col); var bytes=new byte[length]; if(length>0) Marshal.Copy(value,bytes,0,length); row[col]=Encoding.UTF8.GetString(bytes);} rows.Add(row);
                }
                if(code!=101) throw new IOException("The mGBA library is busy or could not be read. Close mGBA and retry."); return rows;
            } catch(DllNotFoundException) {throw new IOException("The Windows SQLite reader is unavailable. Use the ROM folder scan instead.");}
            finally {if(statement!=IntPtr.Zero) sqlite3_finalize(statement); if(db!=IntPtr.Zero) sqlite3_close(db);}
        }
    }
}
