//namespace DotNetty.Buffers
//{
//    using DotNetty.Common.Utilities;

//    public class BitapSearchProcessorFactory : AbstractSearchProcessorFactory
//    {
//        public sealed class Processor : ISearchProcessor
//        {
//            private readonly long[] _bitMasks;
//            private readonly long _successBit;
//            private long _currentMask;

//            internal Processor(long[] bitMasks, long successBit)
//            {
//                _bitMasks = bitMasks;
//                _successBit = successBit;
//            }

//            public bool Process(byte value)
//            {
//                throw new System.NotImplementedException();
//            }

//            public void Reset()
//            {
//                _currentMask = 0L;
//            }
//        }

//        public override ISearchProcessor NewSearchProcessor()
//        {
//            return null;
//        }
//    }
//}