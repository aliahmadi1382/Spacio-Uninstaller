using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spacio
{
    // Windows Shell API. Recycling vetoes a permanent fallback. Permanent deletion
    // is available only through the explicitly confirmed cleanup workflow.
    public static class Recycler
    {
        public static Task<List<string>> RecycleAsync(List<Entry> entries, Snapshot snapshot, IntPtr owner, CancellationToken cancel)
        {
            var source = new TaskCompletionSource<List<string>>();
            var thread = new Thread(delegate()
            {
                var outcomes = new List<string>();
                try
                {
                    foreach (var entry in entries)
                    {
                        cancel.ThrowIfCancellationRequested();
                        try
                        {
                            string blocked = Safety.BlockReason(entry, snapshot);
                            if (blocked != null) throw new InvalidOperationException(blocked);
                            Paths.RequireLocal(entry.Path);
                            Scanner.VerifyUnchanged(entry, cancel);
                            RecycleOne(entry.Path, owner);
                            outcomes.Add("✓ " + entry.Path);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception error) { outcomes.Add("✕ " + entry.Path + Environment.NewLine + L.T(error.Message)); }
                    }
                    source.SetResult(outcomes);
                }
                catch (OperationCanceledException) { source.SetCanceled(); }
                catch (Exception error) { source.SetException(error); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return source.Task;
        }

        private static void RecycleOne(string path, IntPtr owner)
        {
            DeleteReviewedPath(path, owner, false);
        }
        internal static void DeleteReviewedPath(string path, IntPtr owner, bool permanent)
        {
            IFileOperation operation = null; IShellItem item = null;
            var sink = new RecycleSink(permanent);
            try
            {
                operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));
                // RECYCLEONDELETE | ADDUNDORECORD | EARLYFAILURE | NOERRORUI | SILENT |
                // NOCONFIRMATION | NO_CONNECTED_ELEMENTS. Never request elevation.
                Check(operation.SetOperationFlags((permanent ? 0u : 0x00080000u | 0x20000000u) | 0x00100000 | 0x0400 | 0x0004 | 0x0010 | 0x2000));
                Check(operation.SetOwnerWindow(owner));
                Guid iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
                Check(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item));
                Check(operation.DeleteItem(item, sink));
                Check(operation.PerformOperations());
                bool aborted; Check(operation.GetAnyOperationsAborted(out aborted));
                if (sink.PermanentDeleteBlocked) throw new InvalidOperationException("حذف دائمی مجاز نیست؛ ویندوز امکان بازیافت این مورد را ندارد.");
                if (sink.Result < 0) Marshal.ThrowExceptionForHR(sink.Result);
                if (aborted || !sink.Succeeded || System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) throw new InvalidOperationException("عملیات لغو شد یا کامل نشد.");
            }
            finally
            {
                if (item != null) Marshal.ReleaseComObject(item);
                if (operation != null) Marshal.ReleaseComObject(operation);
                GC.KeepAlive(sink);
            }
        }
        private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem { }

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperation
    {
        [PreserveSig] int Advise(IFileOperationProgressSink sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(IntPtr dialog);
        [PreserveSig] int SetProperties(IntPtr properties);
        [PreserveSig] int SetOwnerWindow(IntPtr owner);
        [PreserveSig] int ApplyPropertiesToItem(IShellItem item);
        [PreserveSig] int ApplyPropertiesToItems(IntPtr items);
        [PreserveSig] int RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink sink);
        [PreserveSig] int RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink sink);
        [PreserveSig] int MoveItems(IntPtr items, IShellItem destination);
        [PreserveSig] int CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink sink);
        [PreserveSig] int CopyItems(IntPtr items, IShellItem destination);
        [PreserveSig] int DeleteItem(IShellItem item, IFileOperationProgressSink sink);
        [PreserveSig] int DeleteItems(IntPtr items);
        [PreserveSig] int NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, IFileOperationProgressSink sink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperationProgressSink
    {
        [PreserveSig] int StartOperations();
        [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem newItem);
        [PreserveSig] int PreMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem newItem);
        [PreserveSig] int PreCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem newItem);
        [PreserveSig] int PreDeleteItem(uint flags, IShellItem item);
        [PreserveSig] int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem newItem);
        [PreserveSig] int PreNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int PostNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IShellItem newItem);
        [PreserveSig] int UpdateProgress(uint total, uint completed);
        [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer();
        [PreserveSig] int ResumeTimer();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class RecycleSink : IFileOperationProgressSink
    {
        private readonly bool permanent;
        public RecycleSink() { }
        internal RecycleSink(bool allowPermanent) { permanent = allowPermanent; }
        public bool PermanentDeleteBlocked, Succeeded;
        public int Result;
        public int PreDeleteItem(uint flags, IShellItem item)
        {
            // TSF_DELETE_RECYCLE_IF_POSSIBLE is set only for a recycling operation.
            if (!permanent && (flags & 0x80) == 0) { PermanentDeleteBlocked = true; return unchecked((int)0x80004004); }
            return 0;
        }
        public int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem newItem) { Result = result; Succeeded = result >= 0; return 0; }
        public int StartOperations() { return 0; }
        public int FinishOperations(int result) { if (result < 0) Result = result; return 0; }
        public int PreRenameItem(uint f, IShellItem i, string n) { return 0; }
        public int PostRenameItem(uint f, IShellItem i, string n, int r, IShellItem x) { return 0; }
        public int PreMoveItem(uint f, IShellItem i, IShellItem d, string n) { return 0; }
        public int PostMoveItem(uint f, IShellItem i, IShellItem d, string n, int r, IShellItem x) { return 0; }
        public int PreCopyItem(uint f, IShellItem i, IShellItem d, string n) { return 0; }
        public int PostCopyItem(uint f, IShellItem i, IShellItem d, string n, int r, IShellItem x) { return 0; }
        public int PreNewItem(uint f, IShellItem d, string n) { return 0; }
        public int PostNewItem(uint f, IShellItem d, string n, string t, uint a, int r, IShellItem x) { return 0; }
        public int UpdateProgress(uint t, uint c) { return 0; }
        public int ResetTimer() { return 0; }
        public int PauseTimer() { return 0; }
        public int ResumeTimer() { return 0; }
    }
}
