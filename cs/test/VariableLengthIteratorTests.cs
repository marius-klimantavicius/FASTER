﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FASTER.core;
using NUnit.Framework;

namespace FASTER.test
{
    [TestFixture]
    public class IteratorTests
    {
        [Test]
        public void ShouldSkipEmptySpaceAtEndOfPage()
        {
            var vlLength = new VLValue();
            var log = Devices.CreateLogDevice(TestContext.CurrentContext.TestDirectory + "\\hlog-vl-iter.log", deleteOnClose: true);
            var fht = new FasterKV<Key, VLValue, Input, int[], Empty, VLFunctions>
                (128, new VLFunctions(),
                new LogSettings { LogDevice = log, MemorySizeBits = 17, PageSizeBits = 10 }, // 1KB page
                null, null, null, new VariableLengthStructSettings<Key, VLValue> { valueLength = vlLength }
                );
            fht.StartSession();

            try
            {
                var key = new Key() { key = 1L };
                ref var value = ref GetValue(200, 1);
                fht.Upsert(ref key, ref value, Empty.Default, 0); // page#0

                key = new Key() { key = 2L };
                value = ref GetValue(200, 2);
                fht.Upsert(ref key, ref value, Empty.Default, 0); // page#1 because there is not enough space in page#0

                var len = 1024; // fill page#1 exactly
                len = len - 2 * RecordInfo.GetLength() - 2 * 8 - vlLength.GetLength(ref value);

                key = new Key() { key = 3 };
                value = ref GetValue(len / 4, 3); // should be in page#1
                fht.Upsert(ref key, ref value, Empty.Default, 0);

                key = new Key() { key = 4 };
                value = ref GetValue(64, 4);
                fht.Upsert(ref key, ref value, Empty.Default, 0);

                fht.CompletePending(true);

                var data = new List<Tuple<long, int, int>>();
                using (var iterator = fht.Log.Scan(fht.Log.BeginAddress, fht.Log.TailAddress))
                {
                    while (iterator.GetNext(out var info))
                    {
                        ref var scanKey = ref iterator.GetKey();
                        ref var scanValue = ref iterator.GetValue();

                        data.Add(Tuple.Create(scanKey.key, scanValue.length, scanValue.field1));
                    }
                }

                Assert.AreEqual(4, data.Count);

                Assert.AreEqual(200, data[1].Item2);
                Assert.AreEqual(2, data[1].Item3);

                Assert.AreEqual(3, data[2].Item1);
                Assert.AreEqual(3, data[2].Item3);

                Assert.AreEqual(4, data[3].Item1);
                Assert.AreEqual(64, data[3].Item2);
                Assert.AreEqual(4, data[3].Item3);
            }
            finally
            {
                fht.StopSession();
                fht.Dispose();
                fht = null;
                log.Close();
            }
        }

        private static ref VLValue GetValue(int length, int tag)
        {
            var data = new byte[length * 4];
            ref var value = ref Unsafe.As<byte, VLValue>(ref data[0]);
            value.length = length;
            value.field1 = tag;
            return ref value;
        }
    }
}
