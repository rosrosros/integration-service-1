﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Vertica.Integration.Infrastructure
{
    [Obsolete("Will be removed.")]
    public struct Repeat : IEnumerable<int>
	{
		private readonly int _times;

		private Repeat(int times)
		{
			_times = times;
		}

		public static explicit operator Repeat(uint times)
		{
			return new Repeat((int)times);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<int> GetEnumerator()
		{
			return Enumerable.Range(1, _times).GetEnumerator();
		}

		public static readonly Repeat Twice = (Repeat)2;

        [Obsolete("Will be removed.")]
        public static Repeat Times(uint value)
		{
			return (Repeat)value;
		}

		public override string ToString()
		{
			return $"{_times} time{(_times == 1 ? string.Empty : "s")}";
		}
	}
}