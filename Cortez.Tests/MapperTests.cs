using System.Linq;
using Xunit;

namespace Cortez.Tests
{
	public class MapperTests
	{
		Mapper mapper = new Mapper();

		[Fact]
		public void It_maps_A_to_B() {
			var a = new A{String = "hello world", Int = 42};
			var b = mapper.Map<A, B>(a);

			Assert.NotNull (b);
			Assert.Equal (a.String, b.String);
			Assert.Equal (a.Int, b.Int);
		}

		[Fact]
		public void It_gets_an_expression_to_map_A_to_B() {
			mapper.Map<A, B>();
			var expr = mapper.GetMapExpression<A, B>();

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
			var b = mapper.Map<AggregateA, AggregateB>(a);

			Assert.NotNull (b);
			Assert.NotNull (b.Prop);
			Assert.Equal (a.Id, b.Id);
			Assert.Equal (a.Prop.Int, b.Prop.Int);
			Assert.Equal (a.Prop.String, b.Prop.String);
		}

		[Fact]
		public void It_gets_an_expression_to_map_aggregate_to_aggregate () {
			mapper.Map<AggregateA, AggregateB> ();
			var expr = mapper.GetMapExpression<AggregateA, AggregateB> ();

			var objects = new[] {
				new AggregateA {Prop=new A{String="hello world", Int=42}},
				new AggregateA{Prop=new A{String="Magic", Int=8}}
			};
			var mapped = objects.AsQueryable ().Select (expr);

			Assert.False (mapped.Any (x => x.Prop == null || x.Prop.String == null));
		}

		[Fact]
		public void I_can_instruct_it_how_to_map_a_property() {
			mapper.Map<A, B>(config => config.Set(x => x.String).EqualTo(x => "from B: " + x.Int.ToString()));
			mapper.Map<AggregateA, AggregateB>();
				//(from, to) => to.Prop == new A{String = "from B: " + from.Prop}));

			var result = mapper.Map<AggregateA, AggregateB>(new AggregateA{Id = 63, Prop=new A{String="bar", Int = 42}});
			Assert.NotNull (result.Prop);
			Assert.Equal("from B: 42", result.Prop.String);
			Assert.Equal(42, result.Prop.Int);
			Assert.Equal(63, result.Id);
		}

		[Fact]
		public void It_automaps_when_I_havent_setup_a_mapping () {
			mapper.Map<A, B> (config => config.Set (x => x.String).EqualTo (x => "from B: " + x.Int.ToString ()));

			var result = mapper.Map<AggregateA, AggregateB> (new AggregateA{Id = 63, Prop=new A{String="bar", Int = 42}});
			Assert.NotNull (result.Prop);
			Assert.Equal ("from B: 42", result.Prop.String);
			Assert.Equal (42, result.Prop.Int);
			Assert.Equal (63, result.Id);
		}

		[Fact]
		public void I_can_setup_my_own_constructor_for_a_property() {
			mapper.Map<AggregateA, AggregateB>(config => config.Set(x => x.Prop).EqualTo(x => new B { String = "customized as: " + x.Prop.Int.ToString() }));

			var result = mapper.Map<AggregateA, AggregateB>(new AggregateA{Id = 63, Prop=new A{String="bar", Int = 42}});
			Assert.NotNull (result.Prop);
			Assert.Equal("customized as: 42", result.Prop.String);
			Assert.Equal(0, result.Prop.Int); // We didn't set the other property of `new B`
			Assert.Equal(63, result.Id);
		}
	}
}

