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
			CreateMap<TIn, TOut>(config);
		}

		public void Map<TIn, TOut>() {
			CreateMap<TIn, TOut>(new MapConfiguration<TIn, TOut>());
		}

		void CreateMap<TIn, TOut>(MapConfiguration<TIn, TOut> config) {
			var key = Tuple.Create (typeof (TIn), typeof (TOut));
			if (_functions.ContainsKey(key)) return;

			var fnExpr = CreateMap(typeof (TIn), typeof (TOut), config);
			_functions[key] = fnExpr.Compile();
		}

		LambdaExpression CreateMap(Type typeIn, Type typeOut, IPropertyMap properties) {
			var propMatches = from tin in typeIn.GetProperties()
							  join tout in typeOut.GetProperties() on tin.Name equals tout.Name
							  select Tuple.Create(tin, tout);

			var inParam = Expression.Parameter (typeIn, "input");

			Func<Expression, Expression> getInitExpr = param => 
			{
				var assignments = propMatches.Select (pair => Expression.Bind (
					pair.Item2, 
					GetPropertyValue(param, pair.Item1, pair.Item2.PropertyType, properties)
				)).ToArray();

				return Expression.MemberInit(
					Expression.New (typeOut),
					assignments
				);
			};

			_expressions[Tuple.Create (typeIn, typeOut)] = getInitExpr;
			var initExpr = getInitExpr(inParam);
			return Expression.Lambda(initExpr, inParam);
		}

		Expression GetPropertyValue (Expression param, PropertyInfo source, Type destType, IPropertyMap properties)
		{
			var memberAccess = Expression.MakeMemberAccess (param, source);
			Func<Expression, Expression> fn;
			if (!properties.Maps.TryGetValue (source, out fn)) {
				if (source.PropertyType.Namespace.StartsWith ("System") && destType.Namespace.StartsWith ("System"))
					return memberAccess;

				var key = Tuple.Create (source.PropertyType, destType);
				if (!_expressions.TryGetValue (key, out fn)) {
					CreateMap (source.PropertyType, destType, properties);
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
				Maps[propertyInfo] = param => new SubstituteVisitor(assignment.Parameters[1], param).Visit(body);
				return this;
			}

			Dictionary<PropertyInfo, Func<Expression, Expression>> _maps = new Dictionary<PropertyInfo, Func<Expression, Expression>>();
			public Dictionary<PropertyInfo, Func<Expression, Expression>> Maps { get { return _maps; } }
		}

		class SubstituteVisitor : ExpressionVisitor {
			public readonly Expression From, To;
			public SubstituteVisitor(Expression from, Expression to) {
				From = from;
				To = to;
			}

			public override Expression Visit (Expression node) {
				if (node == From) return To;
				return base.Visit (node);
			}
		}
	}

	namespace Configuration 
	{
		public interface IMapConfiguration<TSource, TDest>
		{
			IMapConfiguration<TSource, TDest> Member(Expression<Func<TSource, TDest, bool>> assignmentSelector);
		}
	}
}

