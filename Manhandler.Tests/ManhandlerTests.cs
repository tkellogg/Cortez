using System.Linq;
using Xunit;

namespace Manhandler.Tests
{
	public class ManhandlerTests
	{
		Manhandler mh = new Manhandler();

		[Fact]
		public void It_maps_A_to_B() {
			var a = new A{String = "hello world", Int = 42};
			var b = mh.Map<A, B>(a);

			Assert.NotNull (b);
			Assert.Equal (a.String, b.String);
			Assert.Equal (a.Int, b.Int);
		}

		[Fact]
		public void It_gets_an_expression_to_map_A_to_B() {
			mh.Map<A, B>();
			var expr = mh.GetMapExpression<A, B>();

			var objects = new[]{new A{String="hello world", Int=42}, new A{String="Magic", Int=8}};
			var mapped = objects.AsQueryable().Select(expr);

			Assert.False (mapped.Any (x => x.String == null));
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
			var a = new AggregateA{Id = 4770, Prop = new A {String = "hello world", Int = 42}};
			var b = mh.Map<AggregateA, AggregateB>(a);

			Assert.NotNull (b);
			Assert.NotNull (b.Prop);
			Assert.Equal (a.Id, b.Id);
			Assert.Equal (a.Prop.Int, b.Prop.Int);
			Assert.Equal (a.Prop.String, b.Prop.String);
		}

		[Fact]
		public void It_gets_an_expression_to_map_aggregate_to_aggregate () {
			mh.Map<AggregateA, AggregateB> ();
			var expr = mh.GetMapExpression<AggregateA, AggregateB> ();

			var objects = new[] {
				new AggregateA {Prop=new A{String="hello world", Int=42}},
				new AggregateA{Prop=new A{String="Magic", Int=8}}
			};
			var mapped = objects.AsQueryable ().Select (expr);

			Assert.False (mapped.Any (x => x.Prop == null || x.Prop.String == null));
		}

		[Fact]
		public void I_can_instruct_it_how_to_map_a_property() {
			mh.Map<AggregateA, AggregateB>(config => config.Member (
				(from, to) => from.Prop == new A{String = "from B: " + to.Prop}));

			var result = mh.Map<AggregateA, AggregateB>(new AggregateA{Id = 63, Prop=new A{String="bar"}});
			Assert.NotNull (result.Prop);
			Assert.Equal("from B: bar", result.Prop.String);
		}
	}
}

