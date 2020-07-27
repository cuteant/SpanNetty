
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class HpackDynamicTableTest
    {
        [Fact]
        public void TestLength()
        {
            HpackDynamicTable table = new HpackDynamicTable(100);
            Assert.Equal(0, table.Length());
            HpackHeaderField entry = new HpackHeaderField((AsciiString)"foo", (AsciiString)"bar");
            table.Add(entry);
            Assert.Equal(1, table.Length());
            table.Clear();
            Assert.Equal(0, table.Length());
        }

        [Fact]
        public void TestSize()
        {
            HpackDynamicTable table = new HpackDynamicTable(100);
            Assert.Equal(0, table.Size());
            HpackHeaderField entry = new HpackHeaderField((AsciiString)"foo", (AsciiString)"bar");
            table.Add(entry);
            Assert.Equal(entry.Size(), table.Size());
            table.Clear();
            Assert.Equal(0, table.Size());
        }

        [Fact]
        public void TestGetEntry()
        {
            HpackDynamicTable table = new HpackDynamicTable(100);
            HpackHeaderField entry = new HpackHeaderField((AsciiString)"foo", (AsciiString)"bar");
            table.Add(entry);
            Assert.Equal(entry, table.GetEntry(1));
            table.Clear();
            try
            {
                table.GetEntry(1);
                Assert.False(true);
            }
            catch (IndexOutOfRangeException)
            {
                //success
            }
        }

        [Fact]
        public void TestGetEntryExceptionally()
        {
            HpackDynamicTable table = new HpackDynamicTable(1);
            Assert.Throws<IndexOutOfRangeException>(() => table.GetEntry(1));
        }

        [Fact]
        public void TestRemove()
        {
            HpackDynamicTable table = new HpackDynamicTable(100);
            Assert.Null(table.Remove());
            HpackHeaderField entry1 = new HpackHeaderField((AsciiString)"foo", (AsciiString)"bar");
            HpackHeaderField entry2 = new HpackHeaderField((AsciiString)"hello", (AsciiString)"world");
            table.Add(entry1);
            table.Add(entry2);
            Assert.Equal(entry1, table.Remove());
            Assert.Equal(entry2, table.GetEntry(1));
            Assert.Equal(1, table.Length());
            Assert.Equal(entry2.Size(), table.Size());
        }

        [Fact]
        public void TestSetCapacity()
        {
            HpackHeaderField entry1 = new HpackHeaderField((AsciiString)"foo", (AsciiString)"bar");
            HpackHeaderField entry2 = new HpackHeaderField((AsciiString)"hello", (AsciiString)"world");
            int size1 = entry1.Size();
            int size2 = entry2.Size();
            HpackDynamicTable table = new HpackDynamicTable(size1 + size2);
            table.Add(entry1);
            table.Add(entry2);
            Assert.Equal(2, table.Length());
            Assert.Equal(size1 + size2, table.Size());
            table.SetCapacity((size1 + size2) * 2); //larger capacity
            Assert.Equal(2, table.Length());
            Assert.Equal(size1 + size2, table.Size());
            table.SetCapacity(size2); //smaller capacity
                                      //entry1 will be removed
            Assert.Equal(1, table.Length());
            Assert.Equal(size2, table.Size());
            Assert.Equal(entry2, table.GetEntry(1));
            table.SetCapacity(0); //clear all
            Assert.Equal(0, table.Length());
            Assert.Equal(0, table.Size());
        }
    }
}
