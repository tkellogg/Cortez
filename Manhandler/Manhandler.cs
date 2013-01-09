using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Manhandler
{
	public class Manhandler
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

		public void Map<TIn, TOut>() {
			var key = Tuple.Create (typeof (TIn), typeof (TOut));
			if (_functions.ContainsKey(key)) return;

			var fnExpr = CreateMap(typeof (TIn), typeof (TOut));
			_functions[key] = fnExpr.Compile();
		}

		LambdaExpression CreateMap(Type typeIn, Type typeOut) {
			var propMatches = from tin in typeIn.GetProperties()
							  join tout in typeOut.GetProperties() on tin.Name equals tout.Name
							  select Tuple.Create(tin, tout);

			var inParam = Expression.Parameter (typeIn, "input");

			Func<Expression, Expression> getInitExpr = param => 
			{
				var assignments = propMatches.Select (pair => Expression.Bind (
					pair.Item2, 
					GetPropertyValue(param, pair.Item1, pair.Item2.PropertyType)
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

		Expression GetPropertyValue(Expression param, PropertyInfo source, Type destType) {
			var memberAccess = Expression.MakeMemberAccess(param, source);
			if (source.PropertyType.Namespace.StartsWith("System") && destType.Namespace.StartsWith("System"))
				return memberAccess;

			Func<Expression, Expression> fn;
			var key = Tuple.Create (source.PropertyType, destType);
			if (!_expressions.TryGetValue (key, out fn)) {
				CreateMap(source.PropertyType, destType);
				fn = _expressions[key];
			}
			return fn(memberAccess);
		}
	}
}

