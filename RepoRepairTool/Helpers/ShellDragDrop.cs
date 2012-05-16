#region DragDropLibCore\DragDropHelper.cs

namespace DragDropLib
{
    using System;
    using System.Runtime.InteropServices;

    [ComImport]
    [Guid("4657278A-411B-11d2-839A-00C04FD918D0")]
    public class DragDropHelper { }
}

#endregion // DragDropLibCore\DragDropHelper.cs

#region DragDropLibCore\IDragSourceHelper.cs

namespace DragDropLib
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    [ComVisible(true)]
    [ComImport]
    [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDragSourceHelper
    {
        void InitializeFromBitmap(
            [In, MarshalAs(UnmanagedType.Struct)] ref ShDragImage dragImage,
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);

        void InitializeFromWindow(
            [In] IntPtr hwnd,
            [In] ref Win32Point pt,
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);
    }

    [ComVisible(true)]
    [ComImport]
    [Guid("83E07D0D-0C5F-4163-BF1A-60B274051E40")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDragSourceHelper2
    {
        void InitializeFromBitmap(
            [In, MarshalAs(UnmanagedType.Struct)] ref ShDragImage dragImage,
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);

        void InitializeFromWindow(
            [In] IntPtr hwnd,
            [In] ref Win32Point pt,
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);

        void SetFlags(
            [In] int dwFlags);
    }
}

#endregion // DragDropLibCore\IDragSourceHelper.cs

#region DragDropLibCore\IDropTargetHelper.cs

namespace DragDropLib
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    [ComVisible(true)]
    [ComImport]
    [Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDropTargetHelper
    {
        void DragEnter(
            [In] IntPtr hwndTarget,
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject,
            [In] ref Win32Point pt,
            [In] int effect);

        void DragLeave();

        void DragOver(
            [In] ref Win32Point pt,
            [In] int effect);

        void Drop(
            [In, MarshalAs(UnmanagedType.Interface)] IDataObject dataObject,
            [In] ref Win32Point pt,
            [In] int effect);

        void Show(
            [In] bool show);
    }
}

#endregion // DragDropLibCore\IDropTargetHelper.cs

#region DragDropLibCore\NativeStructures.cs

namespace DragDropLib
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct Win32Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Win32Size
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShDragImage
    {
        public Win32Size sizeDragImage;
        public Win32Point ptOffset;
        public IntPtr hbmpDragImage;
        public int crColorKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Size = 1044)]
    public struct DropDescription
    {
        public int type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szMessage;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szInsert;
    }
}

#endregion // DragDropLibCore\NativeStructures.cs

#region DragDropLibCore\DataObjectExtensions.cs

namespace System.Runtime.InteropServices.ComTypes
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using DragDropLib;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    /// <summary>
    /// Provides extended functionality for the COM IDataObject interface.
    /// </summary>
    public static class ComDataObjectExtensions
    {
        #region DLL imports

        [DllImport("user32.dll")]
        private static extern uint RegisterClipboardFormat(string lpszFormatName);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref ComTypes.STGMEDIUM pmedium);

        [DllImport("ole32.dll")]
        private static extern int CreateStreamOnHGlobal(IntPtr hGlobal, bool fDeleteOnRelease, out IStream ppstm);

        #endregion // DLL imports

        #region Native constants

        // CFSTR_DROPDESCRIPTION
        private const string DropDescriptionFormat = "DropDescription";

        #endregion // Native constants

        /// <summary>
        /// Sets the drop description for the drag image manager.
        /// </summary>
        /// <param name="dataObject">The DataObject to set.</param>
        /// <param name="dropDescription">The drop description.</param>
        public static void SetDropDescription(this IDataObject dataObject, DropDescription dropDescription)
        {
            ComTypes.FORMATETC formatETC;
            FillFormatETC(DropDescriptionFormat, TYMED.TYMED_HGLOBAL, out formatETC);

            // We need to set the drop description as an HGLOBAL.
            // Allocate space ...
            IntPtr pDD = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DropDescription)));
            try
            {
                // ... and marshal the data
                Marshal.StructureToPtr(dropDescription, pDD, false);

                // The medium wraps the HGLOBAL
                System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
                medium.pUnkForRelease = null;
                medium.tymed = ComTypes.TYMED.TYMED_HGLOBAL;
                medium.unionmember = pDD;

                // Set the data
                ComTypes.IDataObject dataObjectCOM = (ComTypes.IDataObject)dataObject;
                dataObjectCOM.SetData(ref formatETC, ref medium, true);
            }
            catch
            {
                // If we failed, we need to free the HGLOBAL memory
                Marshal.FreeHGlobal(pDD);
                throw;
            }
        }

        /// <summary>
        /// Gets the DropDescription format data.
        /// </summary>
        /// <param name="dataObject">The DataObject.</param>
        /// <returns>The DropDescription, if set.</returns>
        public static object GetDropDescription(this IDataObject dataObject)
        {
            ComTypes.FORMATETC formatETC;
            FillFormatETC(DropDescriptionFormat, TYMED.TYMED_HGLOBAL, out formatETC);

            if (0 == dataObject.QueryGetData(ref formatETC))
            {
                ComTypes.STGMEDIUM medium;
                dataObject.GetData(ref formatETC, out medium);
                try
                {
                    return (DropDescription)Marshal.PtrToStructure(medium.unionmember, typeof(DropDescription));
                }
                finally
                {
                    ReleaseStgMedium(ref medium);
                }
            }

            return null;
        }

        // Combination of all non-null TYMEDs
        private const TYMED TYMED_ANY =
            TYMED.TYMED_ENHMF
            | TYMED.TYMED_FILE
            | TYMED.TYMED_GDI
            | TYMED.TYMED_HGLOBAL
            | TYMED.TYMED_ISTORAGE
            | TYMED.TYMED_ISTREAM
            | TYMED.TYMED_MFPICT;

        /// <summary>
        /// Sets up an advisory connection to the data object.
        /// </summary>
        /// <param name="dataObject">The data object on which to set the advisory connection.</param>
        /// <param name="sink">The advisory sink.</param>
        /// <param name="format">The format on which to callback on.</param>
        /// <param name="advf">Advisory flags. Can be 0.</param>
        /// <returns>The ID of the newly created advisory connection.</returns>
        public static int Advise(this IDataObject dataObject, IAdviseSink sink, string format, ADVF advf)
        {
            // Internally, we'll listen for any TYMED
            FORMATETC formatETC;
            FillFormatETC(format, TYMED_ANY, out formatETC);

            int connection;
            int hr = dataObject.DAdvise(ref formatETC, advf, sink, out connection);
            if (hr != 0)
                Marshal.ThrowExceptionForHR(hr);
            return connection;
        }

        /// <summary>
        /// Fills a FORMATETC structure.
        /// </summary>
        /// <param name="format">The format name.</param>
        /// <param name="tymed">The accepted TYMED.</param>
        /// <param name="formatETC">The structure to fill.</param>
        private static void FillFormatETC(string format, TYMED tymed, out FORMATETC formatETC)
        {
            formatETC.cfFormat = (short)RegisterClipboardFormat(format);
            formatETC.dwAspect = DVASPECT.DVASPECT_CONTENT;
            formatETC.lindex = -1;
            formatETC.ptd = IntPtr.Zero;
            formatETC.tymed = tymed;
        }

        // Identifies data that we need to do custom marshaling on
        private static readonly Guid ManagedDataStamp = new Guid("D98D9FD6-FA46-4716-A769-F3451DFBE4B4");

        /// <summary>
        /// Sets managed data to a clipboard DataObject.
        /// </summary>
        /// <param name="dataObject">The DataObject to set the data on.</param>
        /// <param name="format">The clipboard format.</param>
        /// <param name="data">The data object.</param>
        /// <remarks>
        /// Because the underlying data store is not storing managed objects, but
        /// unmanaged ones, this function provides intelligent conversion, allowing
        /// you to set unmanaged data into the COM implemented IDataObject.</remarks>
        public static void SetManagedData(this IDataObject dataObject, string format, object data)
        {
            // Initialize the format structure
            ComTypes.FORMATETC formatETC;
            FillFormatETC(format, TYMED.TYMED_HGLOBAL, out formatETC);

            // Serialize/marshal our data into an unmanaged medium
            ComTypes.STGMEDIUM medium;
            GetMediumFromObject(data, out medium);
            try
            {
                // Set the data on our data object
                dataObject.SetData(ref formatETC, ref medium, true);
            }
            catch
            {
                // On exceptions, release the medium
                ReleaseStgMedium(ref medium);
                throw;
            }
        }

        /// <summary>
        /// Gets managed data from a clipboard DataObject.
        /// </summary>
        /// <param name="dataObject">The DataObject to obtain the data from.</param>
        /// <param name="format">The format for which to get the data in.</param>
        /// <returns>The data object instance.</returns>
        public static object GetManagedData(this IDataObject dataObject, string format)
        {
            FORMATETC formatETC;
            FillFormatETC(format, TYMED.TYMED_HGLOBAL, out formatETC);

            // Get the data as a stream
            STGMEDIUM medium;
            dataObject.GetData(ref formatETC, out medium);

            IStream nativeStream;
            try
            {
                int hr = CreateStreamOnHGlobal(medium.unionmember, true, out nativeStream);
                if (hr != 0)
                {
                    return null;
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }


            // Convert the native stream to a managed stream            
            STATSTG statstg;
            nativeStream.Stat(out statstg, 0);
            if (statstg.cbSize > int.MaxValue)
                throw new NotSupportedException();
            byte[] buf = new byte[statstg.cbSize];
            nativeStream.Read(buf, (int)statstg.cbSize, IntPtr.Zero);
            MemoryStream dataStream = new MemoryStream(buf);

            // Check for our stamp
            int sizeOfGuid = Marshal.SizeOf(typeof(Guid));
            byte[] guidBytes = new byte[sizeOfGuid];
            if (dataStream.Length >= sizeOfGuid)
            {
                if (sizeOfGuid == dataStream.Read(guidBytes, 0, sizeOfGuid))
                {
                    Guid guid = new Guid(guidBytes);
                    if (ManagedDataStamp.Equals(guid))
                    {
                        // Stamp matched, so deserialize
                        BinaryFormatter formatter = new BinaryFormatter();
                        Type dataType = (Type)formatter.Deserialize(dataStream);
                        object data2 = formatter.Deserialize(dataStream);
                        if (data2.GetType() == dataType)
                            return data2;
                        else if (data2 is string)
                            return ConvertDataFromString((string)data2, dataType);
                        else
                            return null;
                    }
                }
            }

            // Stamp didn't match... attempt to reset the seek pointer
            if (dataStream.CanSeek)
                dataStream.Position = 0;
            return null;
        }

        #region Helper methods

        /// <summary>
        /// Serializes managed data to an HGLOBAL.
        /// </summary>
        /// <param name="data">The managed data object.</param>
        /// <returns>An STGMEDIUM pointing to the allocated HGLOBAL.</returns>
        private static void GetMediumFromObject(object data, out STGMEDIUM medium)
        {
            // We'll serialize to a managed stream temporarily
            MemoryStream stream = new MemoryStream();

            // Write an indentifying stamp, so we can recognize this as custom
            // marshaled data.
            stream.Write(ManagedDataStamp.ToByteArray(), 0, Marshal.SizeOf(typeof(Guid)));

            // Now serialize the data. Note, if the data is not directly serializable,
            // we'll try type conversion. Also, we serialize the type. That way,
            // during deserialization, we know which type to convert back to, if
            // appropriate.
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, data.GetType());
            formatter.Serialize(stream, GetAsSerializable(data));

            // Now copy to an HGLOBAL
            byte[] bytes = stream.GetBuffer();
            IntPtr p = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, p, bytes.Length);
            }
            catch
            {
                // Make sure to free the memory on exceptions
                Marshal.FreeHGlobal(p);
                throw;
            }

            // Now allocate an STGMEDIUM to wrap the HGLOBAL
            medium.unionmember = p;
            medium.tymed = ComTypes.TYMED.TYMED_HGLOBAL;
            medium.pUnkForRelease = null;
        }

        /// <summary>
        /// Gets a serializable object representing the data.
        /// </summary>
        /// <param name="obj">The data.</param>
        /// <returns>If the data is serializable, then it is returned. Otherwise,
        /// type conversion is attempted. If successful, a string value will be
        /// returned.</returns>
        private static object GetAsSerializable(object obj)
        {
            // If the data is directly serializable, run with it
            if (obj.GetType().IsSerializable)
                return obj;

            // Attempt type conversion to a string, but only if we know it can be converted back
            TypeConverter conv = GetTypeConverterForType(obj.GetType());
            if (conv != null && conv.CanConvertTo(typeof(string)) && conv.CanConvertFrom(typeof(string)))
                return conv.ConvertToInvariantString(obj);

            throw new NotSupportedException("Cannot serialize the object");
        }

        /// <summary>
        /// Converts data from a string to the specified format.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <param name="dataType">The target data type.</param>
        /// <returns>Returns the converted data instance.</returns>
        private static object ConvertDataFromString(string data, Type dataType)
        {
            TypeConverter conv = GetTypeConverterForType(dataType);
            if (conv != null && conv.CanConvertFrom(typeof(string)))
                return conv.ConvertFromInvariantString(data);

            throw new NotSupportedException("Cannot convert data");
        }

        /// <summary>
        /// Gets a TypeConverter instance for the specified type.
        /// </summary>
        /// <param name="dataType">The type.</param>
        /// <returns>An instance of a TypeConverter for the type, if one exists.</returns>
        private static TypeConverter GetTypeConverterForType(Type dataType)
        {
            TypeConverterAttribute[] typeConverterAttrs = (TypeConverterAttribute[])dataType.GetCustomAttributes(typeof(TypeConverterAttribute), true);
            if (typeConverterAttrs.Length > 0)
            {
                Type convType = Type.GetType(typeConverterAttrs[0].ConverterTypeName);
                return (TypeConverter)Activator.CreateInstance(convType);
            }

            return null;
        }

        #endregion // Helper methods
    }
}

#endregion // DragDropLibCore\DataObjectExtensions.cs

#region DragDropLibCore\DataObject.cs

namespace DragDropLib
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;

    /// <summary>
    /// Implements the COM version of IDataObject including SetData.
    /// </summary>
    /// <remarks>
    /// <para>Use this object when using shell (or other unmanged) features
    /// that utilize the clipboard and/or drag and drop.</para>
    /// <para>The System.Windows.DataObject (.NET 3.0) and
    /// System.Windows.Forms.DataObject do not support SetData from their COM
    /// IDataObject interface implementation.</para>
    /// <para>To use this object with .NET drag and drop, create an instance
    /// of System.Windows.DataObject (.NET 3.0) or System.Window.Forms.DataObject
    /// passing an instance of DataObject as the only constructor parameter. For
    /// example:</para>
    /// <code>
    /// System.Windows.DataObject data = new System.Windows.DataObject(new DragDropLib.DataObject());
    /// </code>
    /// </remarks>
    [ComVisible(true)]
    public class DataObject : IDataObject, IDisposable
    {
        #region Unmanaged functions

        // These are helper functions for managing STGMEDIUM structures

        [DllImport("urlmon.dll")]
        private static extern int CopyStgMedium(ref STGMEDIUM pcstgmedSrc, ref STGMEDIUM pstgmedDest);
        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

        #endregion // Unmanaged functions

        // Our internal storage is a simple list
        private IList<KeyValuePair<FORMATETC, STGMEDIUM>> storage;

        // Keeps a progressive unique connection id
        private int nextConnectionId = 1;

        // List of advisory connections
        private IDictionary<int, AdviseEntry> connections;

        // Represents an advisory connection entry.
        private class AdviseEntry
        {
            public FORMATETC format;
            public ADVF advf;
            public IAdviseSink sink;

            public AdviseEntry(ref FORMATETC format, ADVF advf, IAdviseSink sink)
            {
                this.format = format;
                this.advf = advf;
                this.sink = sink;
            }
        }

        /// <summary>
        /// Creates an empty instance of DataObject.
        /// </summary>
        public DataObject()
        {
            storage = new List<KeyValuePair<FORMATETC, STGMEDIUM>>();
            connections = new Dictionary<int, AdviseEntry>();
        }

        /// <summary>
        /// Releases unmanaged resources.
        /// </summary>
        ~DataObject()
        {
            Dispose(false);
        }

        /// <summary>
        /// Clears the internal storage array.
        /// </summary>
        /// <remarks>
        /// ClearStorage is called by the IDisposable.Dispose method implementation
        /// to make sure all unmanaged references are released properly.
        /// </remarks>
        private void ClearStorage()
        {
            foreach (KeyValuePair<FORMATETC, STGMEDIUM> pair in storage)
            {
                STGMEDIUM medium = pair.Value;
                ReleaseStgMedium(ref medium);
            }
            storage.Clear();
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        /// <param name="disposing">Indicates if the call was made by a managed caller, or the garbage collector.
        /// True indicates that someone called the Dispose method directly. False indicates that the garbage collector
        /// is finalizing the release of the object instance.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // No managed objects to release
            }

            // Always release unmanaged objects
            ClearStorage();
        }

        #region COM IDataObject Members

        #region COM constants

        private const int OLE_E_ADVISENOTSUPPORTED = unchecked((int)0x80040003);

        private const int DV_E_FORMATETC = unchecked((int)0x80040064);
        private const int DV_E_TYMED = unchecked((int)0x80040069);
        private const int DV_E_CLIPFORMAT = unchecked((int)0x8004006A);
        private const int DV_E_DVASPECT = unchecked((int)0x8004006B);

        #endregion // COM constants

        #region Unsupported functions

        public int EnumDAdvise(out IEnumSTATDATA enumAdvise)
        {
            throw Marshal.GetExceptionForHR(OLE_E_ADVISENOTSUPPORTED);
        }

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
        {
            formatOut = formatIn;
            return DV_E_FORMATETC;
        }

        #endregion // Unsupported functions

        /// <summary>
        /// Adds an advisory connection for the specified format.
        /// </summary>
        /// <param name="pFormatetc">The format for which this sink is called for changes.</param>
        /// <param name="advf">Advisory flags to specify callback behavior.</param>
        /// <param name="adviseSink">The IAdviseSink to call for this connection.</param>
        /// <param name="connection">Returns the new connection's ID.</param>
        /// <returns>An HRESULT.</returns>
        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
        {
            // Check that the specified advisory flags are supported.
            const ADVF ADVF_ALLOWED = ADVF.ADVF_NODATA | ADVF.ADVF_ONLYONCE | ADVF.ADVF_PRIMEFIRST;
            if ((int)((advf | ADVF_ALLOWED) ^ ADVF_ALLOWED) != 0)
            {
                connection = 0;
                return OLE_E_ADVISENOTSUPPORTED;
            }

            // Create and insert an entry for the connection list
            AdviseEntry entry = new AdviseEntry(ref pFormatetc, advf, adviseSink);
            connections.Add(nextConnectionId, entry);
            connection = nextConnectionId;
            nextConnectionId++;

            // If the ADVF_PRIMEFIRST flag is specified and the data exists,
            // raise the DataChanged event now.
            if ((advf & ADVF.ADVF_PRIMEFIRST) == ADVF.ADVF_PRIMEFIRST)
            {
                KeyValuePair<FORMATETC, STGMEDIUM> dataEntry;
                if (GetDataEntry(ref pFormatetc, out dataEntry))
                    RaiseDataChanged(connection, ref dataEntry);
            }

            // S_OK
            return 0;
        }

        /// <summary>
        /// Removes an advisory connection.
        /// </summary>
        /// <param name="connection">The connection id to remove.</param>
        public void DUnadvise(int connection)
        {
            connections.Remove(connection);
        }

        /// <summary>
        /// Gets an enumerator for the formats contained in this DataObject.
        /// </summary>
        /// <param name="direction">The direction of the data.</param>
        /// <returns>An instance of the IEnumFORMATETC interface.</returns>
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            // We only support GET
            if (DATADIR.DATADIR_GET == direction)
                return new EnumFORMATETC(storage);

            throw new NotImplementedException("OLE_S_USEREG");
        }

        /// <summary>
        /// Gets the specified data.
        /// </summary>
        /// <param name="format">The requested data format.</param>
        /// <param name="medium">When the function returns, contains the requested data.</param>
        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            medium = new STGMEDIUM();
            GetDataHere(ref format, ref medium);
        }

        /// <summary>
        /// Gets the specified data.
        /// </summary>
        /// <param name="format">The requested data format.</param>
        /// <param name="medium">When the function returns, contains the requested data.</param>
        /// <remarks>Differs from GetData only in that the STGMEDIUM storage is
        /// allocated and owned by the caller.</remarks>
        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
        {
            // Locate the data
            KeyValuePair<FORMATETC, STGMEDIUM> dataEntry;
            if (GetDataEntry(ref format, out dataEntry))
            {
                STGMEDIUM source = dataEntry.Value;
                medium = CopyMedium(ref source);
                return;
            }

            // Didn't find it. Return an empty data medium.
            medium = default(STGMEDIUM);
        }

        /// <summary>
        /// Determines if data of the requested format is present.
        /// </summary>
        /// <param name="format">The request data format.</param>
        /// <returns>Returns the status of the request. If the data is present, S_OK is returned.
        /// If the data is not present, an error code with the best guess as to the reason is returned.</returns>
        public int QueryGetData(ref FORMATETC format)
        {
            // We only support CONTENT aspect
            if ((DVASPECT.DVASPECT_CONTENT & format.dwAspect) == 0)
                return DV_E_DVASPECT;

            int ret = DV_E_TYMED;

            // Try to locate the data
            // TODO: The ret, if not S_OK, is only relevant to the last item
            foreach (KeyValuePair<FORMATETC, STGMEDIUM> pair in storage)
            {
                if ((pair.Key.tymed & format.tymed) > 0)
                {
                    if (pair.Key.cfFormat == format.cfFormat)
                    {
                        // Found it, return S_OK;
                        return 0;
                    }
                    else
                    {
                        // Found the medium type, but wrong format
                        ret = DV_E_CLIPFORMAT;
                    }
                }
                else
                {
                    // Mismatch on medium type
                    ret = DV_E_TYMED;
                }
            }

            return ret;
        }

        /// <summary>
        /// Sets data in the specified format into storage.
        /// </summary>
        /// <param name="formatIn">The format of the data.</param>
        /// <param name="medium">The data.</param>
        /// <param name="release">If true, ownership of the medium's memory will be transferred
        /// to this object. If false, a copy of the medium will be created and maintained, and
        /// the caller is responsible for the memory of the medium it provided.</param>
        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
        {
            // If the format exists in our storage, remove it prior to resetting it
            foreach (KeyValuePair<FORMATETC, STGMEDIUM> pair in storage)
            {
                if ((pair.Key.tymed & formatIn.tymed) > 0
                    && pair.Key.dwAspect == formatIn.dwAspect
                    && pair.Key.cfFormat == formatIn.cfFormat)
                {
                    STGMEDIUM releaseMedium = pair.Value;
                    ReleaseStgMedium(ref releaseMedium);
                    storage.Remove(pair);
                    break;
                }
            }

            // If release is true, we'll take ownership of the medium.
            // If not, we'll make a copy of it.
            STGMEDIUM sm = medium;
            if (!release)
                sm = CopyMedium(ref medium);

            // Add it to the internal storage
            KeyValuePair<FORMATETC, STGMEDIUM> addPair = new KeyValuePair<FORMATETC, STGMEDIUM>(formatIn, sm);
            storage.Add(addPair);

            RaiseDataChanged(ref addPair);
        }

        /// <summary>
        /// Creates a copy of the STGMEDIUM structure.
        /// </summary>
        /// <param name="medium">The data to copy.</param>
        /// <returns>The copied data.</returns>
        private STGMEDIUM CopyMedium(ref STGMEDIUM medium)
        {
            STGMEDIUM sm = new STGMEDIUM();
            int hr = CopyStgMedium(ref medium, ref sm);
            if (hr != 0)
                throw Marshal.GetExceptionForHR(hr);

            return sm;
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Gets a data entry by the specified format.
        /// </summary>
        /// <param name="pFormatetc">The format to locate the data entry for.</param>
        /// <param name="dataEntry">The located data entry.</param>
        /// <returns>True if the data entry was found, otherwise False.</returns>
        private bool GetDataEntry(ref FORMATETC pFormatetc, out KeyValuePair<FORMATETC, STGMEDIUM> dataEntry)
        {
            foreach (KeyValuePair<FORMATETC, STGMEDIUM> entry in storage)
            {
                FORMATETC format = entry.Key;
                if (IsFormatCompatible(ref pFormatetc, ref format))
                {
                    dataEntry = entry;
                    return true;
                }
            }

            // Not found... default allocate the out param
            dataEntry = default(KeyValuePair<FORMATETC, STGMEDIUM>);
            return false;
        }

        /// <summary>
        /// Raises the DataChanged event for the specified connection.
        /// </summary>
        /// <param name="connection">The connection id.</param>
        /// <param name="dataEntry">The data entry for which to raise the event.</param>
        private void RaiseDataChanged(int connection, ref KeyValuePair<FORMATETC, STGMEDIUM> dataEntry)
        {
            AdviseEntry adviseEntry = connections[connection];
            FORMATETC format = dataEntry.Key;
            STGMEDIUM medium;
            if ((adviseEntry.advf & ADVF.ADVF_NODATA) != ADVF.ADVF_NODATA)
                medium = dataEntry.Value;
            else
                medium = default(STGMEDIUM);

            adviseEntry.sink.OnDataChange(ref format, ref medium);

            if ((adviseEntry.advf & ADVF.ADVF_ONLYONCE) == ADVF.ADVF_ONLYONCE)
                connections.Remove(connection);
        }

        /// <summary>
        /// Raises the DataChanged event for any advisory connections that
        /// are listening for it.
        /// </summary>
        /// <param name="dataEntry">The relevant data entry.</param>
        private void RaiseDataChanged(ref KeyValuePair<FORMATETC, STGMEDIUM> dataEntry)
        {
            foreach (KeyValuePair<int, AdviseEntry> connection in connections)
            {
                if (IsFormatCompatible(connection.Value.format, dataEntry.Key))
                    RaiseDataChanged(connection.Key, ref dataEntry);
            }
        }

        /// <summary>
        /// Determines if the formats are compatible.
        /// </summary>
        /// <param name="format1">A FORMATETC.</param>
        /// <param name="format2">A FORMATETC.</param>
        /// <returns>True if the formats are compatible, otherwise False.</returns>
        /// <remarks>Compatible formats have the same DVASPECT, same format ID, and share
        /// at least one TYMED.</remarks>
        private bool IsFormatCompatible(FORMATETC format1, FORMATETC format2)
        {
            return IsFormatCompatible(ref format1, ref format2);
        }

        /// <summary>
        /// Determines if the formats are compatible.
        /// </summary>
        /// <param name="format1">A FORMATETC.</param>
        /// <param name="format2">A FORMATETC.</param>
        /// <returns>True if the formats are compatible, otherwise False.</returns>
        /// <remarks>Compatible formats have the same DVASPECT, same format ID, and share
        /// at least one TYMED.</remarks>
        private bool IsFormatCompatible(ref FORMATETC format1, ref FORMATETC format2)
        {
            return ((format1.tymed & format2.tymed) > 0
                    && format1.dwAspect == format2.dwAspect
                    && format1.cfFormat == format2.cfFormat);
        }

        #endregion // Helper methods

        #region EnumFORMATETC class

        /// <summary>
        /// Helps enumerate the formats available in our DataObject class.
        /// </summary>
        [ComVisible(true)]
        private class EnumFORMATETC : IEnumFORMATETC
        {
            // Keep an array of the formats for enumeration
            private FORMATETC[] formats;
            // The index of the next item
            private int currentIndex = 0;

            /// <summary>
            /// Creates an instance from a list of key value pairs.
            /// </summary>
            /// <param name="storage">List of FORMATETC/STGMEDIUM key value pairs</param>
            internal EnumFORMATETC(IList<KeyValuePair<FORMATETC, STGMEDIUM>> storage)
            {
                // Get the formats from the list
                formats = new FORMATETC[storage.Count];
                for (int i = 0; i < formats.Length; i++)
                    formats[i] = storage[i].Key;
            }

            /// <summary>
            /// Creates an instance from an array of FORMATETC's.
            /// </summary>
            /// <param name="formats">Array of formats to enumerate.</param>
            private EnumFORMATETC(FORMATETC[] formats)
            {
                // Get the formats as a copy of the array
                this.formats = new FORMATETC[formats.Length];
                formats.CopyTo(this.formats, 0);
            }

            #region IEnumFORMATETC Members

            /// <summary>
            /// Creates a clone of this enumerator.
            /// </summary>
            /// <param name="newEnum">When this function returns, contains a new instance of IEnumFORMATETC.</param>
            public void Clone(out IEnumFORMATETC newEnum)
            {
                EnumFORMATETC ret = new EnumFORMATETC(formats);
                ret.currentIndex = currentIndex;
                newEnum = ret;
            }

            /// <summary>
            /// Retrieves the next elements from the enumeration.
            /// </summary>
            /// <param name="celt">The number of elements to retrieve.</param>
            /// <param name="rgelt">An array to receive the formats requested.</param>
            /// <param name="pceltFetched">An array to receive the number of element fetched.</param>
            /// <returns>If the fetched number of formats is the same as the requested number, S_OK is returned.
            /// There are several reasons S_FALSE may be returned: (1) The requested number of elements is less than
            /// or equal to zero. (2) The rgelt parameter equals null. (3) There are no more elements to enumerate.
            /// (4) The requested number of elements is greater than one and pceltFetched equals null or does not
            /// have at least one element in it. (5) The number of fetched elements is less than the number of
            /// requested elements.</returns>
            public int Next(int celt, FORMATETC[] rgelt, int[] pceltFetched)
            {
                // Start with zero fetched, in case we return early
                if (pceltFetched != null && pceltFetched.Length > 0)
                    pceltFetched[0] = 0;

                // This will count down as we fetch elements
                int cReturn = celt;

                // Short circuit if they didn't request any elements, or didn't
                // provide room in the return array, or there are not more elements
                // to enumerate.
                if (celt <= 0 || rgelt == null || currentIndex >= formats.Length)
                    return 1; // S_FALSE

                // If the number of requested elements is not one, then we must
                // be able to tell the caller how many elements were fetched.
                if ((pceltFetched == null || pceltFetched.Length < 1) && celt != 1)
                    return 1; // S_FALSE

                // If the number of elements in the return array is too small, we
                // throw. This is not a likely scenario, hence the exception.
                if (rgelt.Length < celt)
                    throw new ArgumentException("The number of elements in the return array is less than the number of elements requested");

                // Fetch the elements.
                for (int i = 0; currentIndex < formats.Length && cReturn > 0; i++, cReturn--, currentIndex++)
                    rgelt[i] = formats[currentIndex];

                // Return the number of elements fetched
                if (pceltFetched != null && pceltFetched.Length > 0)
                    pceltFetched[0] = celt - cReturn;

                // cReturn has the number of elements requested but not fetched.
                // It will be greater than zero, if multiple elements were requested
                // but we hit the end of the enumeration.
                return (cReturn == 0) ? 0 : 1; // S_OK : S_FALSE
            }

            /// <summary>
            /// Resets the state of enumeration.
            /// </summary>
            /// <returns>S_OK</returns>
            public int Reset()
            {
                currentIndex = 0;
                return 0; // S_OK
            }

            /// <summary>
            /// Skips the number of elements requested.
            /// </summary>
            /// <param name="celt">The number of elements to skip.</param>
            /// <returns>If there are not enough remaining elements to skip, returns S_FALSE. Otherwise, S_OK is returned.</returns>
            public int Skip(int celt)
            {
                if (currentIndex + celt > formats.Length)
                    return 1; // S_FALSE

                currentIndex += celt;
                return 0; // S_OK
            }

            #endregion
        }

        #endregion // EnumFORMATETC class
    }
}

#endregion // DragDropLibCore\DataObject.cs

#region WpfDragDropLib\WpfDragDropLibExtensions.cs

namespace DragDropLib
{
    using System;
    using System.Windows;

    public static class WpfDragDropLibExtensions
    {
        /// <summary>
        /// Converts a System.Windows.Point value to a DragDropLib.Win32Point value.
        /// </summary>
        /// <param name="pt">Input value.</param>
        /// <returns>Converted value.</returns>
        public static Win32Point ToWin32Point(this Point pt)
        {
            Win32Point wpt = new Win32Point();
            wpt.x = (int)pt.X;
            wpt.y = (int)pt.Y;
            return wpt;
        }
    }
}

#endregion // WpfDragDropLib\WpfDragDropLibExtensions.cs

#region WpfDragDropLib\WpfDragSourceHelper.cs

namespace System.Windows
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using DragDropLib;
    using ComTypes = System.Runtime.InteropServices.ComTypes;

    /// <summary>
    /// Provides helper methods for working with the Shell drag image manager.
    /// </summary>
    public static class DragSourceHelper
    {
        #region DLL imports

        [DllImport("user32.dll")]
        private static extern void PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion // DLL imports

        #region Native constants

        // This is WM_USER + 3. The window controlled by the drag image manager
        // looks for this message (WM_USER + 2 seems to work, too) to invalidate
        // the drag image.
        private const uint WM_INVALIDATEDRAGIMAGE = 0x403;

        // CFSTR_DROPDESCRIPTION
        private const string DropDescriptionFormat = "DropDescription";

        // The drag image manager sets this flag to indicate if the current
        // drop target supports drag images.
        private const string IsShowingLayeredFormat = "IsShowingLayered";

        #endregion // Native constants

        /// <summary>
        /// Internally used to track information about the current drop description.
        /// </summary>
        [Flags]
        private enum DropDescriptionFlags
        {
            None = 0,
            IsDefault = 1,
            InvalidateRequired = 2
        }

        /// <summary>
        /// Keeps a cached drag source context, keyed on the drag source control.
        /// </summary>
        private static IDictionary<UIElement, DragSourceEntry> s_dataContext = new Dictionary<UIElement, DragSourceEntry>();

        /// <summary>
        /// Represents a drag source context entry.
        /// </summary>
        private class DragSourceEntry
        {
            public IDataObject data;
            public int adviseConnection = 0;

            public DragSourceEntry(IDataObject data)
            {
                this.data = data;
            }
        }

        /// <summary>
        /// Keeps drop description info for a data object.
        /// </summary>
        private static IDictionary<IDataObject, DropDescriptionFlags> s_dropDescriptions = new Dictionary<IDataObject, DropDescriptionFlags>();

        /// <summary>
        /// Creates a default DataObject with an internal COM callable implemetation of IDataObject.
        /// </summary>
        /// <returns>A new instance of System.Windows.Forms.IDataObject.</returns>
        public static IDataObject CreateDataObject()
        {
            return new System.Windows.DataObject(new DragDropLib.DataObject());
        }

        /// <summary>
        /// Creates a DataObject with an internal COM callable implementation of IDataObject.
        /// This override also sets the drag image to the specified Bitmap and sets a flag
        /// on the system IDragSourceHelper2 to allow drop descriptions.
        /// </summary>
        /// <param name="dragImage">A Bitmap from which to create the drag image.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <returns>A new instance of System.Windows.Forms.IDataObject.</returns>
        public static IDataObject CreateDataObject(BitmapSource dragImage, Point cursorOffset)
        {
            IDataObject data = CreateDataObject();
            AllowDropDescription(true);
            data.SetDragImage(dragImage, cursorOffset);
            return data;
        }

        /// <summary>
        /// Creates a DataObject with an internal COM callable implementation of IDataObject.
        /// This override also sets the drag image to a bitmap created from the specified
        /// Control instance's UI. It also sets a flag on the system IDragSourceHelper2 to
        /// allow drop descriptions.
        /// </summary>
        /// <param name="element">A UIElement to initialize the drag image from.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <returns>A new instance of System.Windows.Forms.IDataObject.</returns>
        public static IDataObject CreateDataObject(UIElement element, Point cursorOffset)
        {
            IDataObject data = CreateDataObject();
            AllowDropDescription(true);
            data.SetDragImage(element, cursorOffset);
            return data;
        }

        /// <summary>
        /// Registers a Control as a drag source and provides default implementations of
        /// GiveFeedback and QueryContinueDrag.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement instance.</param>
        /// <param name="data">The DataObject associated to the drag source.</param>
        /// <remarks>Callers must call UnregisterDefaultDragSource when the drag and drop
        /// operation is complete to avoid memory leaks.</remarks>
        public static void RegisterDefaultDragSource(UIElement dragSource, IDataObject data)
        {
            // Cache the drag source and the associated data object
            DragSourceEntry entry = new DragSourceEntry(data);
            if (!s_dataContext.ContainsKey(dragSource))
                s_dataContext.Add(dragSource, entry);
            else
                s_dataContext[dragSource] = entry;

            // We need to listen for drop description changes. If a drop target
            // changes the drop description, we shouldn't provide a default one.
            entry.adviseConnection = ComTypes.ComDataObjectExtensions.Advise(((ComTypes.IDataObject)data), new AdviseSink(data), DropDescriptionFormat, 0);

            // Hook up the default drag source event handlers
            dragSource.GiveFeedback += new GiveFeedbackEventHandler(DefaultGiveFeedbackHandler);
            dragSource.QueryContinueDrag += new QueryContinueDragEventHandler(DefaultQueryContinueDragHandler);
        }

        /// <summary>
        /// Registers a Control as a drag source and provides default implementations of
        /// GiveFeedback and QueryContinueDrag. This override also handles the data object
        /// creation, including initialization of the drag image from the Control.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement instance.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <returns>The created data object.</returns>
        /// <remarks>Callers must call UnregisterDefaultDragSource when the drag and drop
        /// operation is complete to avoid memory leaks.</remarks>
        public static IDataObject RegisterDefaultDragSource(UIElement dragSource, Point cursorOffset)
        {
            IDataObject data = CreateDataObject(dragSource, cursorOffset);
            RegisterDefaultDragSource(dragSource, data);
            return data;
        }

        /// <summary>
        /// Registers a Control as a drag source and provides default implementations of
        /// GiveFeedback and QueryContinueDrag. This override also handles the data object
        /// creation, including initialization of the drag image from the speicified Bitmap.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement instance.</param>
        /// <param name="dragImage">A Bitmap to initialize the drag image from.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <returns>The created data object.</returns>
        /// <remarks>Callers must call UnregisterDefaultDragSource when the drag and drop
        /// operation is complete to avoid memory leaks.</remarks>
        public static IDataObject RegisterDefaultDragSource(UIElement dragSource, BitmapSource dragImage, Point cursorOffset)
        {
            IDataObject data = CreateDataObject(dragImage, cursorOffset);
            RegisterDefaultDragSource(dragSource, data);
            return data;
        }

        /// <summary>
        /// Unregisters a drag source from the internal cache.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement.</param>
        public static void UnregisterDefaultDragSource(UIElement dragSource)
        {
            if (s_dataContext.ContainsKey(dragSource))
            {
                DragSourceEntry entry = s_dataContext[dragSource];
                ComTypes.IDataObject dataObjectCOM = (ComTypes.IDataObject)entry.data;

                // Stop listening to drop description changes
                dataObjectCOM.DUnadvise(entry.adviseConnection);

                // Unhook the default drag source event handlers
                dragSource.GiveFeedback -= new GiveFeedbackEventHandler(DefaultGiveFeedbackHandler);
                dragSource.QueryContinueDrag -= new QueryContinueDragEventHandler(DefaultQueryContinueDragHandler);

                // Remove the entries from our context caches
                s_dataContext.Remove(dragSource);
                s_dropDescriptions.Remove(entry.data);
            }
        }

        /// <summary>
        /// Performs a default drag and drop operation for the specified drag source.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <param name="allowedEffects">The allowed drop effects.</param>
        /// <param name="data">The associated data.</param>
        /// <returns>The accepted drop effects from the completed operation.</returns>
        public static DragDropEffects DoDragDrop(UIElement dragSource, Point cursorOffset, DragDropEffects allowedEffects, params KeyValuePair<string, object>[] data)
        {
            IDataObject dataObject = RegisterDefaultDragSource(dragSource, cursorOffset);
            return DoDragDropInternal(dragSource, dataObject, allowedEffects, data);
        }

        /// <summary>
        /// Performs a default drag and drop operation for the specified drag source.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement.</param>
        /// <param name="dragImage">The Bitmap to initialize the drag image from.</param>
        /// <param name="cursorOffset">The drag image cursor offset.</param>
        /// <param name="allowedEffects">The allowed drop effects.</param>
        /// <param name="data">The associated data.</param>
        /// <returns>The accepted drop effects from the completed operation.</returns>
        public static DragDropEffects DoDragDrop(UIElement dragSource, BitmapSource dragImage, Point cursorOffset, DragDropEffects allowedEffects, params KeyValuePair<string, object>[] data)
        {
            IDataObject dataObject = RegisterDefaultDragSource(dragSource, dragImage, cursorOffset);
            return DoDragDropInternal(dragSource, dataObject, allowedEffects, data);
        }

        /// <summary>
        /// Performs a default drag and drop operation for the specified drag source.
        /// </summary>
        /// <param name="dragSource">The drag source UIElement.</param>
        /// <param name="dataObject">The data object associated to the drag and drop operation.</param>
        /// <param name="allowedEffects">The allowed drop effects.</param>
        /// <param name="data">The associated data.</param>
        /// <returns>The accepted drop effects from the completed operation.</returns>
        private static DragDropEffects DoDragDropInternal(UIElement dragSource, IDataObject dataObject, DragDropEffects allowedEffects, KeyValuePair<string, object>[] data)
        {
            // Set the data onto the data object.
            if (data != null)
            {
                foreach (KeyValuePair<string, object> dataPair in data)
                    dataObject.SetDataEx(dataPair.Key, dataPair.Value);
            }

            try
            {
                return DragDrop.DoDragDrop(dragSource, dataObject, allowedEffects);
            }
            finally
            {
                UnregisterDefaultDragSource(dragSource);
            }
        }

        /// <summary>
        /// Provides a default GiveFeedback event handler for drag sources.
        /// </summary>
        /// <param name="sender">The object that raised the event. Should be set to the drag source.</param>
        /// <param name="e">The event arguments.</param>
        public static void DefaultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
        {
            UIElement dragSource = sender as UIElement;
            if (dragSource != null)
            {
                if (s_dataContext.ContainsKey(dragSource))
                {
                    DefaultGiveFeedback(s_dataContext[dragSource].data, e);
                }
            }
        }

        /// <summary>
        /// Provides a default GiveFeedback event handler for drag sources.
        /// </summary>
        /// <param name="data">The associated data object for the event.</param>
        /// <param name="e">The event arguments.</param>
        public static void DefaultGiveFeedback(IDataObject data, GiveFeedbackEventArgs e)
        {
            // For drop targets that don't set the drop description, we'll
            // set a default one. Drop targets that do set drop descriptions
            // should set an invalid drop description during DragLeave.
            bool setDefaultDropDesc = false;
            bool isDefaultDropDesc = IsDropDescriptionDefault(data);
            DropImageType currentType = DropImageType.Invalid;
            if (!IsDropDescriptionValid(data) || isDefaultDropDesc)
            {
                currentType = GetDropImageType(data);
                setDefaultDropDesc = true;
            }

            if (IsShowingLayered(data))
            {
                // The default drag source implementation uses drop descriptions,
                // so we won't use default cursors.
                e.UseDefaultCursors = false;
                Mouse.SetCursor(Cursors.Arrow);
            }
            else
                e.UseDefaultCursors = true;

            // We need to invalidate the drag image to refresh the drop description.
            // This is tricky to implement correctly, but we try to mimic the Windows
            // Explorer behavior. We internally use a flag to tell us to invalidate
            // the drag image, so if that is set, we'll invalidate. Otherwise, we
            // always invalidate if the drop description was set by the drop target,
            // *or* if the current drop image is not None. So if we set a default
            // drop description to anything but None, we'll always invalidate.
            if (InvalidateRequired(data) || !isDefaultDropDesc || currentType != DropImageType.None)
            {
                InvalidateDragImage(data);

                // The invalidate required flag only lasts for one invalidation
                SetInvalidateRequired(data, false);
            }

            // If the drop description is currently invalid, or if it is a default
            // drop description already, we should check about re-setting it.
            if (setDefaultDropDesc)
            {
                // Only change if the effect changed
                if ((DropImageType)e.Effects != currentType)
                {
                    if (e.Effects == DragDropEffects.Copy)
                        data.SetDropDescription(DropImageType.Copy, "Copy", "");
                    else if (e.Effects == DragDropEffects.Link)
                        data.SetDropDescription(DropImageType.Link, "Link", "");
                    else if (e.Effects == DragDropEffects.Move)
                        data.SetDropDescription(DropImageType.Move, "Move", "");
                    else if (e.Effects == DragDropEffects.None)
                        data.SetDropDescription(DropImageType.None, null, null);
                    SetDropDescriptionIsDefault(data, true);

                    // We can't invalidate now, because the drag image manager won't
                    // pick it up... so we set this flag to invalidate on the next
                    // GiveFeedback event.
                    SetInvalidateRequired(data, true);
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Provides a default handler for the QueryContinueDrag drag source event.
        /// </summary>
        /// <param name="sender">The object that raised the event. Not used internally.</param>
        /// <param name="e">The event arguments.</param>
        public static void DefaultQueryContinueDragHandler(object sender, QueryContinueDragEventArgs e)
        {
            DefaultQueryContinueDrag(e);
        }

        /// <summary>
        /// Provides a default handler for the QueryContinueDrag drag source event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        public static void DefaultQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Sets a flag on the system IDragSourceHelper2 object to allow drop descriptions
        /// on the drag image.
        /// </summary>
        /// <param name="allow">True to allow drop descriptions, otherwise False.</param>
        /// <remarks>Must be called before IDragSourceHelper.InitializeFromBitmap or
        /// IDragSourceHelper.InitializeFromControl is called.</remarks>
        public static void AllowDropDescription(bool allow)
        {
            IDragSourceHelper2 sourceHelper = (IDragSourceHelper2)new DragDropHelper();
            sourceHelper.SetFlags(allow ? 1 : 0);
        }

        /// <summary>
        /// Invalidates the drag image.
        /// </summary>
        /// <param name="dataObject">The data object for which to invalidate the drag image.</param>
        /// <remarks>This call tells the drag image manager to reformat the internal
        /// cached drag image, based on the already set drag image bitmap and current drop
        /// description.</remarks>
        public static void InvalidateDragImage(IDataObject dataObject)
        {
            if (dataObject.GetDataPresent("DragWindow"))
            {
                IntPtr hwnd = GetIntPtrFromData(dataObject.GetData("DragWindow"));
                PostMessage(hwnd, WM_INVALIDATEDRAGIMAGE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        #region Helper methods

        /// <summary>
        /// Gets an IntPtr from data acquired from a data object.
        /// </summary>
        /// <param name="data">The data that contains the IntPtr.</param>
        /// <returns>An IntPtr.</returns>
        private static IntPtr GetIntPtrFromData(object data)
        {
            byte[] buf = null;

            if (data is MemoryStream)
            {
                buf = new byte[4];
                if (4 != ((MemoryStream)data).Read(buf, 0, 4))
                    throw new ArgumentException("Could not read an IntPtr from the MemoryStream");
            }
            if (data is byte[])
            {
                buf = (byte[])data;
                if (buf.Length < 4)
                    throw new ArgumentException("Could not read an IntPtr from the byte array");
            }

            if (buf == null)
                throw new ArgumentException("Could not read an IntPtr from the " + data.GetType().ToString());

            int p = (buf[3] << 24) | (buf[2] << 16) | (buf[1] << 8) | buf[0];
            return new IntPtr(p);
        }

        /// <summary>
        /// Determines if the IsShowingLayered flag is set on the data object.
        /// </summary>
        /// <param name="dataObject">The data object.</param>
        /// <returns>True if the flag is set, otherwise false.</returns>
        private static bool IsShowingLayered(IDataObject dataObject)
        {
            if (dataObject.GetDataPresent(IsShowingLayeredFormat))
            {
                object data = dataObject.GetData(IsShowingLayeredFormat);
                if (data != null)
                    return GetBooleanFromData(data);
            }

            return false;
        }

        /// <summary>
        /// Converts compatible clipboard data to a boolean value.
        /// </summary>
        /// <param name="data">The clipboard data.</param>
        /// <returns>True if the data can be converted to a boolean and is set, otherwise False.</returns>
        private static bool GetBooleanFromData(object data)
        {
            if (data is Stream)
            {
                Stream stream = data as Stream;
                BinaryReader reader = new BinaryReader(stream);
                return reader.ReadBoolean();
            }

            // Anything else isn't supported for now
            return false;
        }

        /// <summary>
        /// Checks if the current drop description, if any, is valid.
        /// </summary>
        /// <param name="dataObject">The DataObject from which to get the drop description.</param>
        /// <returns>True if the drop description is set, and the 
        /// DropImageType is not DropImageType.Invalid.</returns>
        private static bool IsDropDescriptionValid(IDataObject dataObject)
        {
            object data = ComTypes.ComDataObjectExtensions.GetDropDescription((ComTypes.IDataObject)dataObject);
            if (data is DropDescription)
                return (DropImageType)((DropDescription)data).type != DropImageType.Invalid;
            return false;
        }

        /// <summary>
        /// Checks if the IsDefault drop description flag is set for the associated DataObject.
        /// </summary>
        /// <param name="dataObject">The associated DataObject.</param>
        /// <returns>True if the IsDefault flag is set, otherwise False.</returns>
        private static bool IsDropDescriptionDefault(IDataObject dataObject)
        {
            if (s_dropDescriptions.ContainsKey(dataObject))
                return (s_dropDescriptions[dataObject] & DropDescriptionFlags.IsDefault) == DropDescriptionFlags.IsDefault;
            return false;
        }

        /// <summary>
        /// Checks if the InvalidateRequired drop description flag is set for the associated DataObject.
        /// </summary>
        /// <param name="dataObject">The associated DataObject.</param>
        /// <returns>True if the InvalidateRequired flag is set, otherwise False.</returns>
        private static bool InvalidateRequired(IDataObject dataObject)
        {
            if (s_dropDescriptions.ContainsKey(dataObject))
                return (s_dropDescriptions[dataObject] & DropDescriptionFlags.InvalidateRequired) == DropDescriptionFlags.InvalidateRequired;
            return false;
        }

        /// <summary>
        /// Sets the IsDefault drop description flag for the associated DataObject.
        /// </summary>
        /// <param name="dataObject">The associdated DataObject.</param>
        /// <param name="isDefault">True to set the flag, False to unset it.</param>
        private static void SetDropDescriptionIsDefault(IDataObject dataObject, bool isDefault)
        {
            if (isDefault)
                SetDropDescriptionFlag(dataObject, DropDescriptionFlags.IsDefault);
            else
                UnsetDropDescriptionFlag(dataObject, DropDescriptionFlags.IsDefault);
        }

        /// <summary>
        /// Sets the InvalidatedRequired drop description flag for the associated DataObject.
        /// </summary>
        /// <param name="dataObject">The associdated DataObject.</param>
        /// <param name="isDefault">True to set the flag, False to unset it.</param>
        private static void SetInvalidateRequired(IDataObject dataObject, bool required)
        {
            if (required)
                SetDropDescriptionFlag(dataObject, DropDescriptionFlags.InvalidateRequired);
            else
                UnsetDropDescriptionFlag(dataObject, DropDescriptionFlags.InvalidateRequired);
        }

        /// <summary>
        /// Sets a drop description flag.
        /// </summary>
        /// <param name="dataObject">The associated DataObject.</param>
        /// <param name="flag">The drop description flag to set.</param>
        private static void SetDropDescriptionFlag(IDataObject dataObject, DropDescriptionFlags flag)
        {
            if (s_dropDescriptions.ContainsKey(dataObject))
                s_dropDescriptions[dataObject] |= flag;
            else
                s_dropDescriptions.Add(dataObject, flag);
        }

        /// <summary>
        /// Unsets a drop description flag.
        /// </summary>
        /// <param name="dataObject">The associated DataObject.</param>
        /// <param name="flag">The drop description flag to unset.</param>
        private static void UnsetDropDescriptionFlag(IDataObject dataObject, DropDescriptionFlags flag)
        {
            if (s_dropDescriptions.ContainsKey(dataObject))
            {
                DropDescriptionFlags current = s_dropDescriptions[dataObject];
                s_dropDescriptions[dataObject] = (current | flag) ^ flag;
            }
        }

        /// <summary>
        /// Gets the current DropDescription's drop image type.
        /// </summary>
        /// <param name="dataObject">The DataObject.</param>
        /// <returns>The current drop image type.</returns>
        private static DropImageType GetDropImageType(IDataObject dataObject)
        {
            object data = ComTypes.ComDataObjectExtensions.GetDropDescription((ComTypes.IDataObject)dataObject);
            if (data is DropDescription)
                return (DropImageType)((DropDescription)data).type;
            return DropImageType.Invalid;
        }

        #endregion // Helper methods

        #region AdviseSink class

        /// <summary>
        /// Provides an advisory sink for the COM IDataObject implementation.
        /// </summary>
        private class AdviseSink : ComTypes.IAdviseSink
        {
            // The associated data object
            private IDataObject data;

            /// <summary>
            /// Creates an AdviseSink associated to the specified data object.
            /// </summary>
            /// <param name="data">The data object.</param>
            public AdviseSink(IDataObject data)
            {
                this.data = data;
            }

            /// <summary>
            /// Handles DataChanged events from a COM IDataObject.
            /// </summary>
            /// <param name="format">The data format that had a change.</param>
            /// <param name="stgmedium">The data value.</param>
            public void OnDataChange(ref ComTypes.FORMATETC format, ref ComTypes.STGMEDIUM stgmedium)
            {
                // We listen to DropDescription changes, so that we can unset the IsDefault
                // drop description flag.
                object odd = ComTypes.ComDataObjectExtensions.GetDropDescription((ComTypes.IDataObject)data);
                if (odd != null)
                    DragSourceHelper.SetDropDescriptionIsDefault(data, false);
            }

            #region Unsupported callbacks

            public void OnClose()
            {
                throw new NotImplementedException();
            }

            public void OnRename(System.Runtime.InteropServices.ComTypes.IMoniker moniker)
            {
                throw new NotImplementedException();
            }

            public void OnSave()
            {
                throw new NotImplementedException();
            }

            public void OnViewChange(int aspect, int index)
            {
                throw new NotImplementedException();
            }

            #endregion // Unsupported callbacks
        }

        #endregion // AdviseSink class
    }
}

#endregion // WpfDragDropLib\WpfDragSourceHelper.cs

#region WpfDragDropLib\WpfDropTargetHelper.cs

namespace System.Windows
{
    using System;
    using System.Windows;
    using DragDropLib;

    public static class DropTargetHelper
    {
        /// <summary>
        /// Internal instance of the DragDropHelper.
        /// </summary>
        private static IDropTargetHelper s_instance = (IDropTargetHelper)new DragDropHelper();

        static DropTargetHelper()
        {
        }

        /// <summary>
        /// Notifies the DragDropHelper that the specified Window received
        /// a DragEnter event.
        /// </summary>
        /// <param name="window">The Window the received the DragEnter event.</param>
        /// <param name="data">The DataObject containing a drag image.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void DragEnter(Window window, IDataObject data, Point cursorOffset, DragDropEffects effect)
        {
            WpfDropTargetHelperExtensions.DragEnter(s_instance, window, data, cursorOffset, effect);
        }

        /// <summary>
        /// Notifies the DragDropHelper that the specified Window received
        /// a DragEnter event.
        /// </summary>
        /// <param name="window">The Window the received the DragEnter event.</param>
        /// <param name="data">The DataObject containing a drag image.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        /// <param name="descriptionMessage">The drop description message.</param>
        /// <param name="descriptionInsert">The drop description insert.</param>
        /// <remarks>Callers of this DragEnter override should make sure to call
        /// the DragLeave override taking an IDataObject parameter in order to clear
        /// the drop description.</remarks>
        public static void DragEnter(Window window, IDataObject data, Point cursorOffset, DragDropEffects effect, string descriptionMessage, string descriptionInsert)
        {
            data.SetDropDescription((DropImageType)effect, descriptionMessage, descriptionInsert);
            DragEnter(window, data, cursorOffset, effect);
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a DragOver event.
        /// </summary>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void DragOver(Point cursorOffset, DragDropEffects effect)
        {
            WpfDropTargetHelperExtensions.DragOver(s_instance, cursorOffset, effect);
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a DragLeave event.
        /// </summary>
        public static void DragLeave()
        {
            s_instance.DragLeave();
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a DragLeave event.
        /// </summary>
        /// <param name="data">The data object associated to the event.</param>
        public static void DragLeave(IDataObject data)
        {
            data.SetDropDescription(DropImageType.Invalid, null, null);
            DragLeave();
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a DragOver event.
        /// </summary>
        /// <param name="data">The DataObject containing a drag image.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void Drop(IDataObject data, Point cursorOffset, DragDropEffects effect)
        {
            WpfDropTargetHelperExtensions.Drop(s_instance, data, cursorOffset, effect);
        }

        /// <summary>
        /// Tells the DragDropHelper to show or hide the drag image.
        /// </summary>
        /// <param name="show">True to show the image. False to hide it.</param>
        public static void Show(bool show)
        {
            s_instance.Show(show);
        }
    }
}

#endregion // WpfDragDropLib\WpfDropTargetHelper.cs

#region WpfDragDropLib\WpfDropTargetHelperExtensions.cs

namespace DragDropLib
{
    using System;
    using System.Windows;
    using System.Windows.Interop;
    using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

    public static class WpfDropTargetHelperExtensions
    {
        /// <summary>
        /// Notifies the DragDropHelper that the specified Window received
        /// a DragEnter event.
        /// </summary>
        /// <param name="dropHelper">The DragDropHelper instance to notify.</param>
        /// <param name="window">The Window the received the DragEnter event.</param>
        /// <param name="data">The DataObject containing a drag image.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void DragEnter(this IDropTargetHelper dropHelper, Window window, IDataObject data, Point cursorOffset, DragDropEffects effect)
        {
            IntPtr windowHandle = IntPtr.Zero;
            if (window != null)
                windowHandle = (new WindowInteropHelper(window)).Handle;
            Win32Point pt = WpfDragDropLibExtensions.ToWin32Point(cursorOffset);
            dropHelper.DragEnter(windowHandle, (ComIDataObject)data, ref pt, (int)effect);
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a DragOver event.
        /// </summary>
        /// <param name="dropHelper">The DragDropHelper instance to notify.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void DragOver(this IDropTargetHelper dropHelper, Point cursorOffset, DragDropEffects effect)
        {
            Win32Point pt = WpfDragDropLibExtensions.ToWin32Point(cursorOffset);
            dropHelper.DragOver(ref pt, (int)effect);
        }

        /// <summary>
        /// Notifies the DragDropHelper that the current Window received
        /// a Drop event.
        /// </summary>
        /// <param name="dropHelper">The DragDropHelper instance to notify.</param>
        /// <param name="data">The DataObject containing a drag image.</param>
        /// <param name="cursorOffset">The current cursor's offset relative to the window.</param>
        /// <param name="effect">The accepted drag drop effect.</param>
        public static void Drop(this IDropTargetHelper dropHelper, IDataObject data, Point cursorOffset, DragDropEffects effect)
        {
            Win32Point pt = WpfDragDropLibExtensions.ToWin32Point(cursorOffset);
            dropHelper.Drop((ComIDataObject)data, ref pt, (int)effect);
        }
    }
}

#endregion // WpfDragDropLib\WpfDropTargetHelperExtensions.cs

#region WpfDragDropLib\WpfDataObjectExtensions.cs

namespace System.Windows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using DragDropLib;
    using Bitmap = System.Drawing.Bitmap;
    using Color = System.Windows.Media.Color;
    using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
    using ComTypes = System.Runtime.InteropServices.ComTypes;
    using DrawingColor = System.Drawing.Color;
    using DrawingColorPalette = System.Drawing.Imaging.ColorPalette;
    using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
    using DrawingRectangle = System.Drawing.Rectangle;
    using PixelFormat = System.Windows.Media.PixelFormat;

    public enum DropImageType
    {
        Invalid = -1,
        None = 0,
        Copy = (int)DragDropEffects.Copy,
        Move = (int)DragDropEffects.Move,
        Link = (int)DragDropEffects.Link,
        Label = 6,
        Warning = 7
    }

    /// <summary>
    /// Provides extended functionality to the System.Windows.IDataObject interface.
    /// </summary>
    public static class WpfDataObjectExtensions
    {
        #region DLL imports

        [DllImport("gdiplus.dll")]
        private static extern bool DeleteObject(IntPtr hgdi);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref ComTypes.STGMEDIUM pmedium);

        #endregion // DLL imports

        /// <summary>
        /// Sets the drag image by rendering the specified UIElement.
        /// </summary>
        /// <param name="dataObject">The DataObject to set the drag image for.</param>
        /// <param name="element">The element to render as the drag image.</param>
        /// <param name="cursorOffset">The offset of the cursor relative to the UIElement.</param>
        public static void SetDragImage(this IDataObject dataObject, UIElement element, Point cursorOffset)
        {
            Size size = element.RenderSize;

            // Get the device's DPI so we render at full size
            // HACK
            int dpix = 96;
            int dpiy = 96;
            //GetDeviceDpi(element, out dpix, out dpiy);

            // Create our renderer at full size
            RenderTargetBitmap renderSource = new RenderTargetBitmap(
                (int)size.Width, (int)size.Height, dpix, dpiy, PixelFormats.Pbgra32);

            // Render the element
            renderSource.Render(element);

            // Set the drag image by the bitmap source
            SetDragImage(dataObject, renderSource, cursorOffset);
        }

        /// <summary>
        /// Sets the drag image from a BitmapSource.
        /// </summary>
        /// <param name="dataObject">The DataObject on which to set the drag image.</param>
        /// <param name="image">The image source.</param>
        /// <param name="cursorOffset">The offset relative to the bitmap image.</param>
        public static void SetDragImage(this IDataObject dataObject, BitmapSource image, Point cursorOffset)
        {
            // Our internal routine requires an HBITMAP, so we'll convert the
            // BitmapSource to a System.Drawing.Bitmap.
            Bitmap bmp = GetBitmapFromBitmapSource(image, Colors.Magenta);

            // Sets the drag image from a Bitmap
            SetDragImage(dataObject, bmp, cursorOffset);
        }

        /// <summary>
        /// Sets the drag image.
        /// </summary>
        /// <param name="dataObject">The DataObject to set the drag image on.</param>
        /// <param name="image">The drag image.</param>
        /// <param name="cursorOffset">The location of the cursor relative to the image.</param>
        private static void SetDragImage(this IDataObject dataObject, Bitmap bitmap, Point cursorOffset)
        {
            ShDragImage shdi = new ShDragImage();

            Win32Size size;
            size.cx = bitmap.Width;
            size.cy = bitmap.Height;
            shdi.sizeDragImage = size;

            Win32Point wpt;
            wpt.x = (int)cursorOffset.X;
            wpt.y = (int)cursorOffset.Y;
            shdi.ptOffset = wpt;

            shdi.crColorKey = DrawingColor.Magenta.ToArgb();

            // This HBITMAP will be managed by the DragDropHelper
            // as soon as we pass it to InitializeFromBitmap. If we fail
            // to make the hand off, we'll delete it to prevent a mem leak.
            IntPtr hbmp = bitmap.GetHbitmap();
            shdi.hbmpDragImage = hbmp;

            try
            {
                IDragSourceHelper sourceHelper = (IDragSourceHelper)new DragDropHelper();

                try
                {
                    sourceHelper.InitializeFromBitmap(ref shdi, (ComIDataObject)dataObject);
                }
                catch (NotImplementedException ex)
                {
                    throw new Exception("A NotImplementedException was caught. This could be because you forgot to construct your DataObject using a DragDropLib.DataObject", ex);
                }
            }
            catch
            {
                // We failed to initialize the drag image, so the DragDropHelper
                // won't be managing our memory. Release the HBITMAP we allocated.
                DeleteObject(hbmp);
            }
        }

        /// <summary>
        /// Sets the drop description for the drag image manager.
        /// </summary>
        /// <param name="dataObject">The DataObject to set.</param>
        /// <param name="type">The type of the drop image.</param>
        /// <param name="format">The format string for the description.</param>
        /// <param name="insert">The parameter for the drop description.</param>
        /// <remarks>
        /// When setting the drop description, the text can be set in two part,
        /// which will be rendered slightly differently to distinguish the description
        /// from the subject. For example, the format can be set as "Move to %1" and
        /// the insert as "Temp". When rendered, the "%1" in format will be replaced
        /// with "Temp", but "Temp" will be rendered slightly different from "Move to ".
        /// </remarks>
        public static void SetDropDescription(this IDataObject dataObject, DropImageType type, string format, string insert)
        {
            if (format != null && format.Length > 259)
                throw new ArgumentException("Format string exceeds the maximum allowed length of 259.", "format");
            if (insert != null && insert.Length > 259)
                throw new ArgumentException("Insert string exceeds the maximum allowed length of 259.", "insert");

            // Fill the structure
            DropDescription dd;
            dd.type = (int)type;
            dd.szMessage = format;
            dd.szInsert = insert;

            ComTypes.ComDataObjectExtensions.SetDropDescription((ComTypes.IDataObject)dataObject, dd);
        }

        /// <summary>
        /// Sets managed data to a clipboard DataObject.
        /// </summary>
        /// <param name="dataObject">The DataObject to set the data on.</param>
        /// <param name="format">The clipboard format.</param>
        /// <param name="data">The data object.</param>
        /// <remarks>
        /// Because the underlying data store is not storing managed objects, but
        /// unmanaged ones, this function provides intelligent conversion, allowing
        /// you to set unmanaged data into the COM implemented IDataObject.</remarks>
        public static void SetDataEx(this IDataObject dataObject, string format, object data)
        {
            DataFormat dataFormat = DataFormats.GetDataFormat(format);

            // Initialize the format structure
            ComTypes.FORMATETC formatETC = new ComTypes.FORMATETC();
            formatETC.cfFormat = (short)dataFormat.Id;
            formatETC.dwAspect = ComTypes.DVASPECT.DVASPECT_CONTENT;
            formatETC.lindex = -1;
            formatETC.ptd = IntPtr.Zero;

            // Try to discover the TYMED from the format and data
            ComTypes.TYMED tymed = GetCompatibleTymed(format, data);
            // If a TYMED was found, we can use the system DataObject
            // to convert our value for us.
            if (tymed != ComTypes.TYMED.TYMED_NULL)
            {
                formatETC.tymed = tymed;

                // Set data on an empty DataObject instance
                DataObject conv = new DataObject();
                conv.SetData(format, data, true);

                // Now retrieve the data, using the COM interface.
                // This will perform a managed to unmanaged conversion for us.
                ComTypes.STGMEDIUM medium;
                ((ComTypes.IDataObject)conv).GetData(ref formatETC, out medium);
                try
                {
                    // Now set the data on our data object
                    ((ComTypes.IDataObject)dataObject).SetData(ref formatETC, ref medium, true);
                }
                catch
                {
                    // On exceptions, release the medium
                    ReleaseStgMedium(ref medium);
                    throw;
                }
            }
            else
            {
                // Since we couldn't determine a TYMED, this data
                // is likely custom managed data, and won't be used
                // by unmanaged code, so we'll use our custom marshaling
                // implemented by our COM IDataObject extensions.

                ComTypes.ComDataObjectExtensions.SetManagedData((ComTypes.IDataObject)dataObject, format, data);
            }
        }

        /// <summary>
        /// Gets a system compatible TYMED for the given format.
        /// </summary>
        /// <param name="format">The data format.</param>
        /// <param name="data">The data.</param>
        /// <returns>A TYMED value, indicating a system compatible TYMED that can
        /// be used for data marshaling.</returns>
        private static ComTypes.TYMED GetCompatibleTymed(string format, object data)
        {
            if (IsFormatEqual(format, DataFormats.Bitmap) && (data is System.Drawing.Bitmap || data is System.Windows.Media.Imaging.BitmapSource))
                return ComTypes.TYMED.TYMED_GDI;
            if (IsFormatEqual(format, DataFormats.EnhancedMetafile))
                return ComTypes.TYMED.TYMED_ENHMF;
            if (IsFormatEqual(format, System.Windows.Ink.StrokeCollection.InkSerializedFormat))
                return ComTypes.TYMED.TYMED_ISTREAM;
            if (data is Stream
                || IsFormatEqual(format, DataFormats.Html) || IsFormatEqual(format, DataFormats.Xaml)
                || IsFormatEqual(format, DataFormats.Text) || IsFormatEqual(format, DataFormats.Rtf)
                || IsFormatEqual(format, DataFormats.OemText)
                || IsFormatEqual(format, DataFormats.UnicodeText) || IsFormatEqual(format, "ApplicationTrust")
                || IsFormatEqual(format, DataFormats.FileDrop)
                || IsFormatEqual(format, "FileName")
                || IsFormatEqual(format, "FileNameW"))
                return ComTypes.TYMED.TYMED_HGLOBAL;
            if (IsFormatEqual(format, DataFormats.Dib) && data is System.Drawing.Image)
                return System.Runtime.InteropServices.ComTypes.TYMED.TYMED_NULL;
            if (IsFormatEqual(format, typeof(System.Windows.Media.Imaging.BitmapSource).FullName)
                || IsFormatEqual(format, typeof(System.Drawing.Bitmap).FullName))
                return ComTypes.TYMED.TYMED_HGLOBAL;
            if (IsFormatEqual(format, DataFormats.EnhancedMetafile) || data is System.Drawing.Imaging.Metafile)
                return System.Runtime.InteropServices.ComTypes.TYMED.TYMED_NULL;
            if (IsFormatEqual(format, DataFormats.Serializable) || (data is System.Runtime.Serialization.ISerializable)
                || ((data != null) && data.GetType().IsSerializable))
                return ComTypes.TYMED.TYMED_HGLOBAL;

            return ComTypes.TYMED.TYMED_NULL;
        }

        /// <summary>
        /// Compares the equality of two clipboard formats.
        /// </summary>
        /// <param name="formatA">First format.</param>
        /// <param name="formatB">Second format.</param>
        /// <returns>True if the formats are equal. False otherwise.</returns>
        private static bool IsFormatEqual(string formatA, string formatB)
        {
            return string.CompareOrdinal(formatA, formatB) == 0;
        }

        /// <summary>
        /// Gets managed data from a clipboard DataObject.
        /// </summary>
        /// <param name="dataObject">The DataObject to obtain the data from.</param>
        /// <param name="format">The format for which to get the data in.</param>
        /// <returns>The data object instance.</returns>
        public static object GetDataEx(this IDataObject dataObject, string format)
        {
            // Get the data
            object data = dataObject.GetData(format, true);

            // If the data is a stream, we'll check to see if it
            // is stamped by us for custom marshaling
            if (data is Stream)
            {
                object data2 = ComTypes.ComDataObjectExtensions.GetManagedData((ComTypes.IDataObject)dataObject, format);
                if (data2 != null)
                    return data2;
            }

            return data;
        }

        #region Helper methods

        /// <summary>
        /// Gets the device capabilities.
        /// </summary>
        /// <param name="reference">A reference UIElement for getting the relevant device caps.</param>
        /// <param name="dpix">The horizontal DPI.</param>
        /// <param name="dpiy">The vertical DPI.</param>
        private static void GetDeviceDpi(Visual reference, out int dpix, out int dpiy)
        {
            Matrix m = PresentationSource.FromVisual(reference).CompositionTarget.TransformToDevice;
            dpix = (int)(96 * m.M11);
            dpiy = (int)(96 * m.M22);
        }

        /// <summary>
        /// Gets a System.Drawing.Bitmap from a BitmapSource.
        /// </summary>
        /// <param name="source">The source image from which to create our Bitmap.</param>
        /// <param name="transparencyKey">The transparency key. This is used by the DragDropHelper
        /// in rendering transparent pixels.</param>
        /// <returns>An instance of Bitmap which is a copy of the BitmapSource's image.</returns>
        private static Bitmap GetBitmapFromBitmapSource(BitmapSource source, Color transparencyKey)
        {
            // Copy at full size
            Int32Rect sourceRect = new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight);

            // Convert to our destination pixel format
            DrawingPixelFormat pxFormat = ConvertPixelFormat(source.Format);

            // Create the Bitmap, full size, full rez
            Bitmap bmp = new Bitmap(sourceRect.Width, sourceRect.Height, pxFormat);
            // If the format is an indexed format, copy the color palette
            if ((pxFormat & DrawingPixelFormat.Indexed) == DrawingPixelFormat.Indexed)
                ConvertColorPalette(bmp.Palette, source.Palette);

            // Get the transparency key as a System.Drawing.Color
            DrawingColor transKey = transparencyKey.ToDrawingColor();

            // Lock our Bitmap bits, we need to write to it
            BitmapData bmpData = bmp.LockBits(
                sourceRect.ToDrawingRectangle(),
                ImageLockMode.ReadWrite,
                pxFormat);
            {
                // Copy the source bitmap data to our new Bitmap
                source.CopyPixels(sourceRect, bmpData.Scan0, bmpData.Stride * sourceRect.Height, bmpData.Stride);

                // The drag image seems to work in full 32-bit color, except when
                // alpha equals zero. Then it renders those pixels at black. So
                // we make a pass and set all those pixels to the transparency key
                // color. This is only implemented for 32-bit pixel colors for now.
                if ((pxFormat & DrawingPixelFormat.Alpha) == DrawingPixelFormat.Alpha)
                    ReplaceTransparentPixelsWithTransparentKey(bmpData, transKey);
            }
            // Done, unlock the bits
            bmp.UnlockBits(bmpData);

            return bmp;
        }

        /// <summary>
        /// Replaces any pixel with a zero alpha value with the specified transparency key.
        /// </summary>
        /// <param name="bmpData">The bitmap data in which to perform the operation.</param>
        /// <param name="transKey">The transparency color. This color is rendered transparent
        /// by the DragDropHelper.</param>
        /// <remarks>
        /// This function only supports 32-bit pixel formats for now.
        /// </remarks>
        private static void ReplaceTransparentPixelsWithTransparentKey(BitmapData bmpData, DrawingColor transKey)
        {
            DrawingPixelFormat pxFormat = bmpData.PixelFormat;

            if (DrawingPixelFormat.Format32bppArgb == pxFormat
                || DrawingPixelFormat.Format32bppPArgb == pxFormat)
            {
                int transKeyArgb = transKey.ToArgb();

                // We will just iterate over the data... we don't care about pixel location,
                // just that every pixel is checked.
                unsafe
                {
                    byte* pscan = (byte*)bmpData.Scan0.ToPointer();
                    {
                        for (int y = 0; y < bmpData.Height; ++y, pscan += bmpData.Stride)
                        {
                            int* prgb = (int*)pscan;
                            for (int x = 0; x < bmpData.Width; ++x, ++prgb)
                            {
                                // If the alpha value is zero, replace this pixel's color
                                // with the transparency key.
                                if ((*prgb & 0xFF000000L) == 0L)
                                    *prgb = transKeyArgb;
                            }
                        }
                    }
                }
            }
            else
            {
                // If it is anything else, we aren't supporting it, but we
                // won't throw, cause it isn't an error
                System.Diagnostics.Trace.TraceWarning("Not converting transparent colors to transparency key.");
                return;
            }
        }

        /// <summary>
        /// Converts a System.Windows.Media.Color to System.Drawing.Color.
        /// </summary>
        /// <param name="color">System.Windows.Media.Color value to convert.</param>
        /// <returns>System.Drawing.Color value.</returns>
        private static DrawingColor ToDrawingColor(this Color color)
        {
            return DrawingColor.FromArgb(
                color.A, color.R, color.G, color.B);
        }

        /// <summary>
        /// Converts a System.Windows.Int32Rect to a System.Drawing.Rectangle value.
        /// </summary>
        /// <param name="rect">The System.Windows.Int32Rect to convert.</param>
        /// <returns>The System.Drawing.Rectangle converted value.</returns>
        private static DrawingRectangle ToDrawingRectangle(this Int32Rect rect)
        {
            return new DrawingRectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Converts the entries in a BitmapPalette to ColorPalette entries.
        /// </summary>
        /// <param name="destPalette">ColorPalette destination palette.</param>
        /// <param name="bitmapPalette">BitmapPalette source palette.</param>
        private static void ConvertColorPalette(DrawingColorPalette destPalette, BitmapPalette bitmapPalette)
        {
            DrawingColor[] destEntries = destPalette.Entries;
            IList<Color> sourceEntries = bitmapPalette.Colors;

            if (destEntries.Length < sourceEntries.Count)
                throw new ArgumentException("Destination palette has less entries than the source palette");

            for (int i = 0, count = sourceEntries.Count; i < count; ++i)
                destEntries[i] = sourceEntries[i].ToDrawingColor();
        }

        /// <summary>
        /// Converts a System.Windows.Media.PixelFormat instance to a
        /// System.Drawing.Imaging.PixelFormat value.
        /// </summary>
        /// <param name="pixelFormat">The input PixelFormat.</param>
        /// <returns>The converted value.</returns>
        private static DrawingPixelFormat ConvertPixelFormat(PixelFormat pixelFormat)
        {
            if (PixelFormats.Bgr24 == pixelFormat)
                return DrawingPixelFormat.Format24bppRgb;
            if (PixelFormats.Bgr32 == pixelFormat)
                return DrawingPixelFormat.Format32bppRgb;
            if (PixelFormats.Bgr555 == pixelFormat)
                return DrawingPixelFormat.Format16bppRgb555;
            if (PixelFormats.Bgr565 == pixelFormat)
                return DrawingPixelFormat.Format16bppRgb565;
            if (PixelFormats.Bgra32 == pixelFormat)
                return DrawingPixelFormat.Format32bppArgb;
            if (PixelFormats.BlackWhite == pixelFormat)
                return DrawingPixelFormat.Format1bppIndexed;
            if (PixelFormats.Gray16 == pixelFormat)
                return DrawingPixelFormat.Format16bppGrayScale;
            if (PixelFormats.Indexed1 == pixelFormat)
                return DrawingPixelFormat.Format1bppIndexed;
            if (PixelFormats.Indexed4 == pixelFormat)
                return DrawingPixelFormat.Format4bppIndexed;
            if (PixelFormats.Indexed8 == pixelFormat)
                return DrawingPixelFormat.Format8bppIndexed;
            if (PixelFormats.Pbgra32 == pixelFormat)
                return DrawingPixelFormat.Format32bppPArgb;
            if (PixelFormats.Prgba64 == pixelFormat)
                return DrawingPixelFormat.Format64bppPArgb;
            if (PixelFormats.Rgb24 == pixelFormat)
                return DrawingPixelFormat.Format24bppRgb;
            if (PixelFormats.Rgb48 == pixelFormat)
                return DrawingPixelFormat.Format48bppRgb;
            if (PixelFormats.Rgba64 == pixelFormat)
                return DrawingPixelFormat.Format64bppArgb;

            throw new NotSupportedException("The pixel format of the source bitmap is not supported.");
        }

        #endregion // Helper methods
    }
}

#endregion // WpfDragDropLib\WpfDataObjectExtensions.cs

