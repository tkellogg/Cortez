using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cortez
{
	public class Cortez
	{
		Dictionary<Tuple<Type, Type>, object> _functions = new Dictionary<Tuple<Type, Type>, object>();
		Dictionary<Tuple<Type, Type>, Func<Expression, Expression>> _expressions = 
			new Dictionary<Tuple<Type, Type>, Func<Expression, Expression>>();

		public Func<TIn, TOut> GetMapConstructor<TIn, TOut>() {
			object fn;
			if (_functions.TryGetValue (Tuple.Create(typeof(TIn), typeof(TOut)), out fn)) {
				return (Func<TIn, TOut>) fn;
			}
			return null;
		}

		public TOut Map<TIn, TOut>(TIn input) {
			var fn = GetMapConstructor<TIn, TOut>();
			if (fn == null) {
				Map<TIn, TOut>();
				fn = GetMapConstructor<TIn, TOut>();
			}
			return fn(input);
		}

		public Expression<Func<TIn, TOut>> GetMapExpression<TIn, TOut>() {
			Func<Expression, Expression> expr;
			_expressions.TryGetValue(Tuple.Create(typeof (TIn), typeof (TOut)), out expr);
			var parameter = Expression.Parameter(typeof (TIn), "input");
			var lambda = Expression.Lambda (expr(parameter), parameter);
			return (Expression<Func<TIn, TOut>>) lambda;
		}

		public void Map<TIn, TOut> (Action<Configuration.IMapConfiguration<TIn, TOut>> expr) {
			var config = new MapConfiguration<TIn, TOut>();
			expr(config);
			CreateMapAndCacheFullExpresion<TIn, TOut>(config);
		}

		public void Map<TIn, TOut>() {
			CreateMapAndCacheFullExpresion<TIn, TOut>(new MapConfiguration<TIn, TOut>());
		}

		void CreateMapAndCacheFullExpresion<TIn, TOut>(MapConfiguration<TIn, TOut> config) {
			var key = Tuple.Create (typeof (TIn), typeof (TOut));
			if (_functions.ContainsKey(key)) return;

			var fnExpr = CreateMapAndCacheFullExpresion(typeof (TIn), typeof (TOut), config);
			_functions[key] = fnExpr.Compile();
		}

		/// <summary>
		/// Creates a `new TSource { Prop1 = ..., Prop2 = ..., ... }` expression. For each
		/// property initialization it calls GetPropertyValue, which can nest a lot deeper.
		/// </summary>
		LambdaExpression CreateMapAndCacheFullExpresion(Type typeIn, Type typeOut, IPropertyMap properties) {
			var propMatches = from tin in typeIn.GetProperties()
							  join tout in typeOut.GetProperties() on tin.Name equals tout.Name
							  select Tuple.Create(tin, tout);

			var inParam = Expression.Parameter (typeIn, "input");

			// As a Func because we need to reuse it later with different parameters
			Func<Expression, Expression> getInitExpr = param => 
			{
				var assignments = propMatches.Select (props => {
					var val = GetPropertyValue(param, props.Item1, props.Item2, properties);
					return Expression.Bind (props.Item2, val);
				}).ToArray();

				return Expression.MemberInit(
					Expression.New (typeOut),
					assignments
				);
			};

			_expressions[Tuple.Create (typeIn, typeOut)] = getInitExpr;
			var initExpr = getInitExpr(inParam);
			return Expression.Lambda(initExpr, inParam);
		}

		/// <summary>
		/// Gets an expression representing the value of a property. This could be a simple
		/// member access, or as complicated as another mapping function.
		/// </summary>
		/// <param name='param'>The object that <c>propIn</c> belongs to</param>
		/// <param name='propIn'>The property that data is flowing from</param>
		/// <param name='typeOut'>The property type of the destination</param>
		/// <param name='properties'></param>
		Expression GetPropertyValue (Expression param, PropertyInfo propIn, PropertyInfo propOut, IPropertyMap properties)
		{
			var typeOut = propOut.PropertyType;
			var memberAccess = Expression.MakeMemberAccess (param, propIn);
			Func<Expression, Expression> fn;
			if (!properties.Maps.TryGetValue (propOut, out fn)) {
				if (propIn.PropertyType.Namespace.StartsWith ("System") && typeOut.Namespace.StartsWith ("System"))
					return memberAccess;

				var key = Tuple.Create (propIn.PropertyType, typeOut);
				if (!_expressions.TryGetValue (key, out fn)) {
					CreateMapAndCacheFullExpresion (propIn.PropertyType, typeOut, properties);
					fn = _expressions [key];
				}
			}
			return fn(memberAccess);
		}

		interface IPropertyMap {
			Dictionary<PropertyInfo, Func<Expression, Expression>> Maps { get; }
		}

		class MapConfiguration<TSource, TDest> : Configuration.IMapConfiguration<TSource, TDest>, IPropertyMap
		{
			public Configuration.IMapConfiguration<TSource, TDest> Member(Expression<Func<TSource, TDest, bool>> assignment) {
				var binExpr = assignment.Body as BinaryExpression;
				if (binExpr == null || binExpr.NodeType != ExpressionType.Equal) return this;
				var memberExpr = binExpr.Left as MemberExpression;
				if (memberExpr == null || memberExpr.NodeType != ExpressionType.MemberAccess) return this;

				var propertyInfo = (PropertyInfo) memberExpr.Member;
				var body = binExpr.Right;
				Maps[propertyInfo] = param => body;
				return this;
			}

			public Configuration.IMapPropertyValue<TSource, TDest, TProperty> Set<TProperty>(Expression<Func<TDest, TProperty>> propertySelector) {
				return new MapPropertyValue<TProperty>(this, propertySelector);
			}

			Dictionary<PropertyInfo, Func<Expression, Expression>> _maps = new Dictionary<PropertyInfo, Func<Expression, Expression>>();
			/// <value>
			/// The Func processes 1 parameter (a member access) and returns 
			/// </value>
			public Dictionary<PropertyInfo, Func<Expression, Expression>> Maps { get { return _maps; } }

			class MapPropertyValue<TProperty> : Configuration.IMapPropertyValue<TSource, TDest, TProperty>
			{
				MapConfiguration<TSource, TDest> _config;
				public PropertyInfo Destination { get; set; }

				public MapPropertyValue(MapConfiguration<TSource, TDest> config, Expression<Func<TDest, TProperty>> destinationSelector) {
					_config = config;

					var memberExpr = destinationSelector.Body as MemberExpression;
					if (memberExpr == null) return;
					Destination = (PropertyInfo) memberExpr.Member;
				}

				public Configuration.IMapConfiguration<TSource,TDest> EqualTo(Expression<Func<TSource, TProperty>> sourceSelector) {
					_config.Maps[Destination] = to => {
						to = FindParameter (to);
						return Substitute(sourceSelector.Body, sourceSelector.Parameters[0], to);
					};
					return _config;
				}
			}
		}

		static ParameterExpression FindParameter(Expression expr) {
			while (expr is MemberExpression) {
				expr = ((MemberExpression)expr).Expression;
			}
			return expr as ParameterExpression;
		}

		static Expression Substitute(Expression @using, Expression find, Expression replaceWith) {
			return new SubstituteVisitor(find, replaceWith).Visit(@using);
		}

		// Not used. I may not use yet yet...
		class SubstituteVisitor : ExpressionVisitor {
			public readonly Expression From, To;
			public SubstituteVisitor(Expression from, Expression to) {
				From = from;
				To = to;
			}

			public override Expression Visit (Expression node) {
				if (node == From) return To;
				var ret = base.Visit (node);
				return ret;
			}
		}
	}

	namespace Configuration 
	{
		public interface IMapConfiguration<TSource, TDest>
		{
			//IMapConfiguration<TSource, TDest> Member(Expression<Func<TSource, TDest, bool>> assignmentSelector);
			IMapPropertyValue<TSource, TDest, TProperty> Set<TProperty>(Expression<Func<TDest, TProperty>> propertySelector);
		}

		public interface IMapPropertyValue<TSource, TDest, TProperty> 
		{
			IMapConfiguration<TSource,TDest> EqualTo(Expression<Func<TSource, TProperty>> valueSelector);
		}
	}
}

