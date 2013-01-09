using System;
using Xunit;

namespace Manhandler.Tests
{
	public class ManhandlerTests
	{
		Manhandler mh = new Manhandler();

		[Fact]
		public void It_maps_A_to_B() {
			mh.Map<A, B>();
			var ctor = mh.GetMapConstructor<A, B>();
			var a = new A{String = "hello world", Int = 42};
			var b = ctor(a);

			Assert.NotNull (b);
			Assert.Equal (a.String, b.String);
			Assert.Equal (a.Int, b.Int);
		}

		class A {
			public string String {get;set;}
			public int Int {get;set;}
		}
		class B {
			public string String {get;set;}
			public int Int {get;set;}
		}
		class AggregateA {
			public int Id {get;set;}
			public A Prop {get;set;}
		}
		class AggregateB {
			public int Id {get;set;}
			public B Prop {get;set;}
		}

		[Fact]
		public void It_maps_aggregate_to_aggregate() {
			mh.Map<AggregateA, AggregateB>();
			var ctor = mh.GetMapConstructor<AggregateA, AggregateB>();
			var a = new AggregateA{Id = 4770, Prop = new A {String = "hello world", Int = 42}};
			var b = ctor(a);

			Assert.NotNull (b);
			Assert.NotNull (b.Prop);
			Assert.Equal (a.Id, b.Id);
			Assert.Equal (a.Prop.Int, b.Prop.Int);
			Assert.Equal (a.Prop.String, b.Prop.String);
		}

	}
}

