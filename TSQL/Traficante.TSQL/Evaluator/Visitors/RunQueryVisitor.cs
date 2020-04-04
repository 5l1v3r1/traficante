﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Traficante.TSQL;
using Traficante.TSQL.Evaluator.Helpers;
using Traficante.TSQL.Evaluator.Utils;
using Traficante.TSQL.Parser;
using Traficante.TSQL.Parser.Nodes;
using Traficante.TSQL.Schema.Helpers;
using Traficante.TSQL.Schema.Managers;
using TextSpan = Traficante.TSQL.Parser.TextSpan;

namespace Traficante.TSQL.Evaluator.Visitors
{
    public class RunQueryVisitor : IExpressionVisitor
    {
        ExpressionHelper expressionHelper = new ExpressionHelper();

        Dictionary<string, Expression> _cte = new Dictionary<string, Expression>();

        Stack<System.Linq.Expressions.Expression> Nodes { get; set; }
        private TSQLEngine _engine;
        private readonly CancellationToken _cancellationToken;
        private QueryState _state;
        private QueryPart _queryPart;

        public object Result = null;



        public RunQueryVisitor(TSQLEngine engine, CancellationToken cancellationToken)
        {
            Nodes = new Stack<System.Linq.Expressions.Expression>();
            this._engine = engine;
            this._cancellationToken = cancellationToken;
        }

        public void SetQueryState(QueryState queryState)
        {
            _state = queryState;
        }

        public void SetQueryPart(QueryPart queryPart)
        {
            _queryPart = queryPart;
        }


        public void Visit(Node node)
        {
        }

        public void Visit(DescNode node)
        {

            if (node.Type == DescForType.SpecificConstructor)
            {
                var fromNode = (FromFunctionNode)node.From;

                var method = _engine.ResolveMethod(fromNode.Function.Name, fromNode.Function.Path, fromNode.Function.ArgumentsTypes);
                Type itemsType = null;
                if (typeof(IEnumerable).IsAssignableFrom(method.FunctionMethod.ReturnType))
                {
                    itemsType = method.FunctionMethod.ReturnType.GetGenericArguments().FirstOrDefault();
                }


                var functionColumns = TypeHelper.GetColumns(itemsType);
                var descType = expressionHelper.CreateAnonymousType(new (string, Type)[3] {
                    ("Name", typeof(string)),
                    ("Index", typeof(int)),
                    ("Type", typeof(string))
                });
                
                var columnsType = typeof(List<>).MakeGenericType(descType);
                var columns = columnsType.GetConstructors()[0].Invoke(new object[0]);
                for (int i = 0; i < functionColumns.Length; i++)
                {
                    var descObj = descType.GetConstructors()[0].Invoke(new object[0]);
                    descType.GetField("Name").SetValue(descObj, functionColumns[i].ColumnName);
                    descType.GetField("Index").SetValue(descObj, functionColumns[i].ColumnIndex);
                    descType.GetField("Type").SetValue(descObj, functionColumns[i].ColumnType.ToString());
                    columnsType.GetMethod("Add", new Type[] { descType }).Invoke(columns, new object[] { descObj });
                }

                Nodes.Push(Expression.Constant(columns));
                return;
            }
            if (node.Type == DescForType.Schema)
            {
                var fromNode = (FromFunctionNode)node.From;

            }
        }

        public void Visit(StarNode node)
        {
            var b = Nodes.Pop();
            var a = Nodes.Pop();
            (a, b) = this.expressionHelper.AlignSimpleTypes(a, b);
            Nodes.Push(Expression.Multiply(a, b));
        }

        public void Visit(FSlashNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            (left, right) = this.expressionHelper.AlignSimpleTypes(left, right);
            Nodes.Push(Expression.Divide(left, right));
        }

        public void Visit(ModuloNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            (left, right) = this.expressionHelper.AlignSimpleTypes(left, right);
            Nodes.Push(Expression.Modulo(left, right));
        }

        public void Visit(AddNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            (left, right) = this.expressionHelper.AlignSimpleTypes(left, right);
            if (node.ReturnType == typeof(string))
            {
                var concatMethod = typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) });
                Nodes.Push(Expression.Add(left, right, concatMethod));
            }
            else
            {
                Nodes.Push(Expression.Add(left, right));
            }
        }

        public void Visit(HyphenNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            (left, right) = this.expressionHelper.AlignSimpleTypes(left, right);
            Nodes.Push(Expression.Subtract(left, right));
        }

        public void Visit(AndNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(Expression.And(left, right));
        }

        public void Visit(OrNode node)
        {
            var left = Nodes.Pop();
            var right = Nodes.Pop();
            Nodes.Push(Expression.Or(left, right));
        }

        public void Visit(EqualityNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.Equal));
        }

        public void Visit(GreaterOrEqualNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.GreaterThanOrEqual));
        }

        public void Visit(LessOrEqualNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.LessThanOrEqual));
        }

        public void Visit(GreaterNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.GreaterThan));
        }

        public void Visit(LessNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.LessThan));
        }

        public void Visit(DiffNode node)
        {
            var right = Nodes.Pop();
            var left = Nodes.Pop();
            Nodes.Push(this.expressionHelper.SqlLikeOperation(left, right, Expression.NotEqual));
        }

        public void Visit(NotNode node)
        {
            Nodes.Push(Expression.Not(Nodes.Pop()));
        }
        public void Visit(LikeNode node)
        {
            Visit(new FunctionNode(nameof(Operators.Like),
                new ArgsListNode(new[] { node.Left, node.Right }),
                new string[0],
                new MethodInfo { FunctionMethod = typeof(Operators).GetMethod(nameof(Operators.Like)) }));
        }

        public void Visit(RLikeNode node)
        {
            Visit(new FunctionNode(nameof(Operators.RLike),
                new ArgsListNode(new[] { node.Left, node.Right }),
                new string[0],
                new MethodInfo { FunctionMethod = typeof(Operators).GetMethod(nameof(Operators.RLike)) }));
        }

        public void Visit(InNode node)
        {
            var right = (ArgsListNode)node.Right;
            var left = node.Left;

            var rightExpressions = new List<Expression>();
            for (int i = 0; i < right.Args.Length; i++)
                rightExpressions.Add(Nodes.Pop());

            var leftExpression = Nodes.Pop();

            Expression exp = this.expressionHelper.SqlLikeOperation(
                leftExpression,
                rightExpressions[0],
                Expression.Equal);
            for (var i = 1; i < rightExpressions.Count; i++)
            {
                exp = Expression.Or(
                    exp,
                    this.expressionHelper.SqlLikeOperation(
                        leftExpression,
                        rightExpressions[i],
                        Expression.Equal)
                    );
            }

            Nodes.Push(exp);
        }

        private FieldNode _currentFieldNode = null;
        public void SetFieldNode(FieldNode node)
        {
            _currentFieldNode = node;
        }

        public void Visit(FieldNode node)
        {
            if ((node.Expression is AllColumnsNode) == false && _queryPart == QueryPart.Select)
            {
                _state.SelectedFieldsNodes.Add(node);
                var value = Nodes.Pop();
                Nodes.Push(Expression.Convert(value, node.ReturnType));
            }
            /// TODO: add check if conversion is needed
            //var value = Nodes.Pop();
            //Nodes.Push(Expression.Convert(value, node.ReturnType));
            Nodes.Push(Nodes.Pop());
        }

        public void Visit(FieldOrderedNode node)
        {
            var value = Nodes.Pop();
            Nodes.Push(value);
        }

        public void Visit(StringNode node)
        {
            Nodes.Push(Expression.Constant(node.ObjValue, node.ReturnType));
        }

        public void Visit(DecimalNode node)
        {
            Nodes.Push(Expression.Constant(node.ObjValue, node.ReturnType));
        }

        public void Visit(IntegerNode node)
        {
            Nodes.Push(Expression.Constant(node.ObjValue, node.ReturnType));
        }

        public void Visit(BooleanNode node)
        {
            Nodes.Push(Expression.Constant(node.ObjValue, node.ReturnType));
        }

        public void Visit(WordNode node)
        {
            Nodes.Push(Expression.Constant(node.ObjValue, node.ReturnType));
        }

        public void Visit(ContainsNode node)
        {
            var rightNode = node.Right as ArgsListNode;
            var rightArgs = Enumerable.Range(0, rightNode.Args.Length).Select(x => Nodes.Pop());
            var right = Expression.NewArrayInit(rightNode.Args[0].ReturnType, rightArgs);
            var rightQueryable = Expression.Call(
                typeof(ParallelEnumerable),
                "AsParallel", new Type[] { rightNode.Args[0].ReturnType }, right);

            var left = Nodes.Pop();

            MethodCallExpression containsCall = Expression.Call(
                typeof(ParallelEnumerable),
                "Contains",
                new Type[] { right.Type.GetElementType() },
                rightQueryable,
                left);

            Nodes.Push(containsCall);
        }

        public void Visit(FunctionNode node)
        {
            var args = Enumerable.Range(0, node.ArgsCount).Select(x => Nodes.Pop()).Reverse().ToArray();
            var argsTypes = args.Select(x => x.Type).ToArray();
            MethodInfo methodInfo = node.Method ?? this._engine.ResolveMethod(node.Name, node.Path, argsTypes);
            node.ChangeMethod(methodInfo);

            if (node.IsAggregateMethod)
            {
                if (this._state.QueryItem.Type.Name == "IGrouping`2")
                {

                    if (node.Method.Name == "Count")
                    {
                        var selector = Expression.Lambda(args[0], this._state.ItemInGroup);
                        var group = Expression.Convert(this._state.QueryItem, typeof(IEnumerable<>).MakeGenericType(this._state.ItemInGroup.Type));
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(Enumerable),
                            "Select",
                            new Type[] { this._state.ItemInGroup.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { group, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(Enumerable),
                            "Count",
                            new Type[] { node.Arguments.Args[0].ReturnType },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Sum")
                    {
                        var selector = Expression.Lambda(args[0], this._state.ItemInGroup);
                        var group = Expression.Convert(this._state.QueryItem, typeof(IEnumerable<>).MakeGenericType(this._state.ItemInGroup.Type));
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(Enumerable),
                            "Select",
                            new Type[] { this._state.ItemInGroup.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { group, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(Enumerable),
                            "Sum",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Max")
                    {
                        var selector = Expression.Lambda(args[0], this._state.ItemInGroup);
                        var group = Expression.Convert(this._state.QueryItem, typeof(IEnumerable<>).MakeGenericType(this._state.ItemInGroup.Type));
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(Enumerable),
                            "Select",
                            new Type[] { this._state.ItemInGroup.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { group, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(Enumerable),
                            "Max",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Min")
                    {
                        var selector = Expression.Lambda(args[0], this._state.ItemInGroup);
                        var group = Expression.Convert(this._state.QueryItem, typeof(IEnumerable<>).MakeGenericType(this._state.ItemInGroup.Type));
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(Enumerable),
                            "Select",
                            new Type[] { this._state.ItemInGroup.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { group, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(Enumerable),
                            "Min",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Avg")
                    {
                        var selector = Expression.Lambda(args[0], this._state.ItemInGroup);
                        var group = Expression.Convert(this._state.QueryItem, typeof(IEnumerable<>).MakeGenericType(this._state.ItemInGroup.Type));
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(Enumerable),
                            "Select",
                            new Type[] { this._state.ItemInGroup.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { group, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(Enumerable),
                            "Average",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    throw new ApplicationException($"Aggregate method  {node.Method.Name} is not supported.");
                }
                else
                {
                    if (node.Method.Name == "Count")
                    {
                        var selector = Expression.Lambda(args[0], this._state.QueryItem);
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Select",
                            new Type[] { this._state.QueryItem.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { this._state.Query, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Count",
                            new Type[] { node.Arguments.Args[0].ReturnType },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Sum")
                    {
                        var selector = Expression.Lambda(args[0], this._state.QueryItem);
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Select",
                            new Type[] { this._state.QueryItem.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { this._state.Query, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Sum",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Avg")
                    {
                        var selector = Expression.Lambda(args[0], this._state.QueryItem);
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Select",
                            new Type[] { this._state.QueryItem.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { this._state.Query, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Average",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Max")
                    {
                        var selector = Expression.Lambda(args[0], this._state.QueryItem);
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Select",
                            new Type[] { this._state.QueryItem.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { this._state.Query, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Max",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    if (node.Method.Name == "Min")
                    {
                        var selector = Expression.Lambda(args[0], this._state.QueryItem);
                        MethodCallExpression selectCall = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Select",
                            new Type[] { this._state.QueryItem.Type, node.Arguments.Args[0].ReturnType },
                            new Expression[] { this._state.Query, selector });
                        MethodCallExpression call = Expression.Call(
                            typeof(ParallelEnumerable),
                            "Min",
                            new Type[] { },
                            new Expression[] { selectCall });
                        Nodes.Push(call);
                        return;
                    }
                    throw new ApplicationException($"Aggregate method  {node.Method.Name} is not supported.");
                }
            }
            else
            {
                if (this._state?.QueryItem?.Type.Name == "IGrouping`2")
                {
                    var key = Expression.PropertyOrField(this._state.QueryItem, "Key");
                    if (key.Type.GetFields().Any(x => string.Equals(x.Name, this._currentFieldNode.FieldName)))
                    {
                        Nodes.Push(Expression.PropertyOrField(key, this._currentFieldNode.FieldName));
                        return;
                    }
                }

                if (node.Name == "RowNumber")
                {
                    Nodes.Push(Expression.Add(this._state.QueryItemIndex, Expression.Constant(1)));
                    return;
                }

                if (methodInfo == null)
                    throw new TSQLException($"Function does not exist: {node.Name}");
                var instance = methodInfo.FunctionMethod.ReflectedType.GetConstructors()[0].Invoke(new object[] { });
                /// TODO: check if there can be more that one generic argument
                var method = methodInfo.FunctionMethod.IsGenericMethodDefinition ?
                    methodInfo.FunctionMethod.MakeGenericMethod(node.ReturnType) : methodInfo.FunctionMethod;

                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = this.expressionHelper.AlignSimpleTypes(args[i], parameters[i].ParameterType);
                }
                var paramsParameter = parameters.FirstOrDefault(x => x.ParameterType.IsArray);
                var paramsParameterIndex = Array.IndexOf(parameters, paramsParameter);
                if (paramsParameter != null)
                {
                    var typeOfParams = paramsParameter.ParameterType.GetElementType();
                    var arrayOfParams = Expression.NewArrayInit(typeOfParams, args.Skip(paramsParameterIndex));
                    args = args.Take(paramsParameterIndex).Concat(new Expression[] { arrayOfParams }).ToArray();
                }

                Nodes.Push(Expression.Call(Expression.Constant(instance), method, args));
            }
        }

        public void Visit(IsNullNode node)
        {
            var currentValue = Nodes.Pop();
            //TODO: check that, Nullable<> is also a value type
            if (currentValue.Type.IsValueType)
            {

                Nodes.Push(Expression.Constant(node.IsNegated));
            }
            else
            {
                var defaultValue = Expression.Default(currentValue.Type);
                if (node.IsNegated)
                {
                    Nodes.Push(Expression.NotEqual(currentValue, defaultValue));
                }
                else
                {
                    Nodes.Push(Expression.Equal(currentValue, defaultValue));
                }
            }
        }

        public void Visit(AccessColumnNode node)
        {
            if (_state.QueryItem.Type.Name == "IGrouping`2")
            {
                // TODO: just for testing. 
                // come with idea, how to figure out if the colum is inside aggregation function 
                // or is just column to display
                //try
                //{
                var fieldNameameWithAlias = node.Alias + "." + node.Name;

                var key = Expression.PropertyOrField(this._state.QueryItem, "Key");
                if (key.Type.GetFields().Any(x => string.Equals(x.Name, fieldNameameWithAlias)))
                {
                    //var properyOfKey = Expression.PropertyOrField(key, fieldNameameWithAlias);
                    var properyOfKey = this.expressionHelper.PropertyOrField(key, fieldNameameWithAlias, node.ReturnType);
                    Nodes.Push(properyOfKey);
                    return;
                }
                else
                if (key.Type.GetFields().Any(x => string.Equals(x.Name, node.Name)))
                {
                    //var properyOfKey = Expression.PropertyOrField(key, node.Name);
                    var properyOfKey = this.expressionHelper.PropertyOrField(key, node.Name, node.ReturnType);
                    Nodes.Push(properyOfKey);
                    return;
                }
                else
                {
                    var aliasProperty = this._state.ItemInGroup.Type.GetFields().FirstOrDefault(x => string.Equals(x.Name, node.Alias));
                    if (aliasProperty != null)
                    {
                        var nameProperty = aliasProperty.FieldType.GetFields().FirstOrDefault(x => string.Equals(x.Name, node.Name));
                        if (nameProperty != null)
                        {
                            var alias = Expression.PropertyOrField(this._state.ItemInGroup, node.Alias);
                            var propertyInAlias = this.expressionHelper.PropertyOrField(alias, node.Name, node.ReturnType);
                            Nodes.Push(propertyInAlias);
                            //Nodes.Push(
                            //    Expression.PropertyOrField(
                            //        Expression.PropertyOrField(this._itemInGroup, node.Alias),
                            //        node.Name));
                            return;
                        }
                    }
                    //var groupItemProperty = Expression.PropertyOrField(this._itemInGroup, node.Name);
                    var groupItemProperty = this.expressionHelper.PropertyOrField(this._state.ItemInGroup, node.Name, node.ReturnType);
                    Nodes.Push(groupItemProperty);
                    return;
                }
                //}
                //catch (Exception ex)
                //{
                //    var groupItemProperty = Expression.PropertyOrField(this._itemInGroup, node.Name);
                //    Nodes.Push(groupItemProperty);
                //}
            }

            if (_state.Alias2QueryItem.ContainsKey(node.Alias))
            {
                var item = _state.Alias2QueryItem[node.Alias];
                var property = this.expressionHelper.PropertyOrField(item, node.Name, node.ReturnType);
                Nodes.Push(property);
            }
            else
            {
                var property = this.expressionHelper.PropertyOrField(_state.QueryItem, node.Name, node.ReturnType);
                Nodes.Push(property);
            }
        }

        public void Visit(AllColumnsNode node)
        {
            var columns = TypeHelper.GetColumns(this._state.QueryItem.Type);
            foreach (var column in columns)
            {
                IdentifierNode identifierNode = new IdentifierNode(column.ColumnName, column.ColumnType);
                FieldNode fieldNode = new FieldNode(identifierNode, -1, column.ColumnName);
                _state.SelectedFieldsNodes.Add(fieldNode);
                Visit(new AccessColumnNode(column.ColumnName, string.Empty, column.ColumnType, TextSpan.Empty));
            }
        }

        public void Visit(IdentifierNode node)
        {
            if (this._state.ItemInGroup != null)
            {
                var columns = TypeHelper.GetColumns(this._state.ItemInGroup.Type);
                var column = columns.FirstOrDefault(x => x.ColumnName == node.Name);
                if (column == null)
                    throw new TSQLException($"Column does not exist: {node.Name}");
                node.ChangeReturnType(column.ColumnType);
                Visit(new AccessColumnNode(node.Name, string.Empty, column.ColumnType, TextSpan.Empty));
            }
            else
            {
                var columns = TypeHelper.GetColumns(this._state.QueryItem.Type);
                var column = columns.FirstOrDefault(x => x.ColumnName == node.Name);
                if (column == null)
                    throw new TSQLException($"Column does not exist: {node.Name}");
                node.ChangeReturnType(column.ColumnType);
                Visit(new AccessColumnNode(node.Name, string.Empty, column.ColumnType, TextSpan.Empty));
            }
        }

        public void Visit(AccessObjectArrayNode node)
        {
            var property = Nodes.Pop();
            var array = Expression.PropertyOrField(property, node.ObjectName);
            var index = Expression.Constant(node.Token.Index);
            Nodes.Push(Expression.ArrayAccess(array, index));
        }

        public void Visit(AccessObjectKeyNode node)
        {
            throw new NotImplementedException();
        }

        public void Visit(PropertyValueNode node)
        {
            var obj = Nodes.Pop();
            Nodes.Push(Expression.Property(obj, node.PropertyInfo));
        }

        public void Visit(VariableNode node)
        {
            var variable = _engine.GetVariable(node.Name);
            Nodes.Push(Expression.Constant(variable?.Value, variable?.Type ?? node.ReturnType));
        }

        public void Visit(DeclareNode node)
        {
            _engine.SetVariable(node.Variable.Name, node.Type.ReturnType, null);
        }

        public void Visit(SetNode node)
        {
            Expression valueExpression = Nodes.Pop();
            var value = Expression.Lambda<Func<object>>(valueExpression).Compile()();
            _engine.SetVariable(node.Variable.Name, value);
        }

        public void Visit(DotNode node)
        {
            if (node.Expression is FunctionNode)
            {
                List<string> accessors = new List<string>();
                Node parentNode = node.Root;
                while (parentNode is null == false)
                {
                    if (parentNode is IdentifierNode)
                        accessors.Add(((IdentifierNode)parentNode).Name);
                    if (parentNode is PropertyValueNode)
                        accessors.Add(((PropertyValueNode)parentNode).Name);
                    if (parentNode is DotNode)
                    {
                        var dot = (DotNode)parentNode;
                        if (dot.Expression is IdentifierNode)
                            accessors.Add(((IdentifierNode)dot.Expression).Name);
                        if (parentNode is PropertyValueNode)
                            accessors.Add(((PropertyValueNode)dot.Expression).Name);
                    }
                    parentNode = (parentNode as DotNode)?.Root;
                }

                accessors.Reverse();

                FunctionNode function = node.Expression as FunctionNode;
                function.ChangePath(accessors.ToArray());
                Visit(function);
                return;
            }

            if (node.Expression is IdentifierNode)
            {
                //var self = node;
                //var theMostInner = self;
                //while (!(self is null))
                //{
                //    theMostInner = self;
                //    self = self.Root as DotNode;
                //}



                IdentifierNode itemNode = (IdentifierNode)node.Expression;
                IdentifierNode rootNode = (IdentifierNode)node.Root;

                var item = _state.Alias2QueryItem[rootNode.Name];
                var columns = TypeHelper.GetColumns(item.Type);
                var column = columns.FirstOrDefault(x => x.ColumnName == itemNode.Name);
                if (column == null)
                    throw new TSQLException($"Column does not exist: {itemNode.Name}");
                itemNode.ChangeReturnType(column.ColumnType);
                Visit(new AccessColumnNode(itemNode.Name, rootNode.Name, column.ColumnType, TextSpan.Empty));
            }

            //var self = node;
            //var theMostInner = self;
            //while (!(self is null))
            //{
            //    theMostInner = self;
            //    self = self.Root as DotNode;
            //}

            //var ident = (IdentifierNode)theMostInner.Root;
        }

        public void Visit(ArgsListNode node)
        {
            //Nodes.Push(node);
        }

        public void Visit(SelectNode node)
        {
            var selectedFieldsNodes = _state.SelectedFieldsNodes;

            var fieldNodes = new Expression[selectedFieldsNodes.Count];
            for (var i = 0; i < selectedFieldsNodes.Count; i++)
                fieldNodes[selectedFieldsNodes.Count - 1 - i] = Nodes.Pop();


            var outputItemType = expressionHelper.CreateAnonymousType(selectedFieldsNodes.Select(x => (x.FieldName, x.ReturnType)).Distinct());

            List<MemberBinding> bindings = new List<MemberBinding>();
            for (int i = 0; i < selectedFieldsNodes.Count; i++)
            {
                var name = selectedFieldsNodes[i].FieldName;
                var value = fieldNodes[i];
                //"SelectProp = inputItem.Prop"
                MemberBinding assignment = Expression.Bind(
                    outputItemType.GetField(name),
                    value);
                bindings.Add(assignment);
            }

            //"new AnonymousType()"
            var creationExpression = Expression.New(outputItemType.GetConstructor(Type.EmptyTypes));

            //"new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            var initialization = Expression.MemberInit(creationExpression, bindings);

            if (_state.IsSingleRowResult())
            {
                var array = Expression.NewArrayInit(outputItemType, new Expression[] { initialization });

                var call = Expression.Call(
                    typeof(ParallelEnumerable),
                    "AsParallel",
                    new Type[] { outputItemType },
                    array);

                if (_state.Query != null)
                    Nodes.Push(Expression.Lambda(call, _state.Query));
                else
                    Nodes.Push(Expression.Lambda(call));
            }
            else
            {
                //"item => new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
                Expression expression = Expression.Lambda(initialization, _state.QueryItem, _state.QueryItemIndex);

                var call = Expression.Call(
                    typeof(ParallelEnumerable),
                    "Select",
                    new Type[] { this._state.QueryItem.Type, outputItemType },
                    _state.Query,
                    expression);

                Nodes.Push(Expression.Lambda(call, _state.Query));
            }

            //"AnonymousType input"
            this._state.QueryItem = Expression.Parameter(outputItemType, "item_" + outputItemType.Name);
            //"IQueryable<AnonymousType> input"
            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(outputItemType), "query");
        }

        public void Visit(WhereNode node)
        {
            //_state.Query.Where((item) =>
            //{
                
            //});

            var predicate = Nodes.Pop();
            var predicateLambda = Expression.Lambda(predicate, this._state.QueryItem);

            MethodCallExpression call = Expression.Call(
                typeof(ParallelEnumerable),
                "Where",
                new Type[] { this._state.QueryItem.Type },
                _state.Query,
                predicateLambda);

            Nodes.Push(Expression.Lambda(call, _state.Query));
        }

        public void Visit(GroupByNode node)
        {
            var outputFields = new (FieldNode Field, Expression Value)[node.Fields.Length];
            for (var i = 0; i < node.Fields.Length; i++)
                outputFields[node.Fields.Length - 1 - i] = (node.Fields[node.Fields.Length - 1 - i], Nodes.Pop());
            var outputItemType = expressionHelper.CreateAnonymousType(outputFields.Select(x => (x.Field.FieldName, x.Field.ReturnType)));

            List<MemberBinding> bindings = new List<MemberBinding>();
            foreach (var field in outputFields)
            {
                //"SelectProp = inputItem.Prop"
                MemberBinding assignment = Expression.Bind(
                    outputItemType.GetField(field.Field.FieldName),
                    field.Value);
                bindings.Add(assignment);
            }

            //"new AnonymousType()"
            var creationExpression = Expression.New(outputItemType.GetConstructor(Type.EmptyTypes));

            //"new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            var initialization = Expression.MemberInit(creationExpression, bindings);

            //"item => new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            Expression expression = Expression.Lambda(initialization, _state.QueryItem);

            var groupByCall = Expression.Call(
                typeof(ParallelEnumerable),
                "GroupBy",
                new Type[] { this._state.QueryItem.Type, outputItemType },
                _state.Query,
                expression);

            Nodes.Push(Expression.Lambda(groupByCall, _state.Query));


            // "ItemAnonymousType itemInGroup "
            this._state.ItemInGroup = Expression.Parameter(this._state.QueryItem.Type, "itemInGroup_" + this._state.QueryItem.Type);

            // "IGrouping<KeyAnonymousType, ItemAnonymousType>"
            outputItemType = typeof(IGrouping<,>).MakeGenericType(outputItemType, this._state.QueryItem.Type);

            // "IGrouping<KeyAnonymousType, ItemAnonymousType> item"
            this._state.QueryItem = Expression.Parameter(outputItemType, "item_" + outputItemType.Name);

            // "IQueryable<IGrouping<KeyAnonymousType, ItemAnonymousType>> input"
            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(outputItemType), "query");
        }

        public void Visit(HavingNode node)
        {
            var predicate = Nodes.Pop();
            var predicateLambda = Expression.Lambda(predicate, this._state.QueryItem);

            MethodCallExpression call = Expression.Call(
                typeof(ParallelEnumerable),
                "Where",
                new Type[] { this._state.QueryItem.Type },
                _state.Query,
                predicateLambda);

            Nodes.Push(Expression.Lambda(call, _state.Query));
        }

        public void Visit(SkipNode node)
        {
            var call = _state.Query.Skip((int)node.Value);
            Nodes.Push(Expression.Lambda(call, _state.Query));
        }

        public void Visit(TakeNode node)
        {
            var call = _state.Query.Take((int)node.Value);
            Nodes.Push(Expression.Lambda(call, _state.Query));
        }

        public void Visit(TopNode node)
        {
            var call = _state.Query.Take((int)node.Value);
            Nodes.Push(Expression.Lambda(call, _state.Query));
        }

        public IEnumerable<Object[]> AsEnumerable(IDataReader source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            List<Object[]> list = new List<object[]>();
            while (source.Read())
            {
                Object[] row = new Object[source.FieldCount];
                source.GetValues(row);
                for (int i = 0; i < source.FieldCount; i++)
                {
                    if (row[i] is DBNull)
                        row[i] = GetDefaultValue(source.GetFieldType(i));
                }
                //yield return row;
                list.Add(row);
            }
            return list;
        }

        public object GetDefaultValue(Type t)
        {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public void Visit(FromFunctionNode node)
        {
            var function = node.Function;
            var method = _engine.ResolveMethod(function.Name, function.Path, function.ArgumentsTypes);

            List<Expression> functionExpressionArgumetns = new List<Expression>();
            for (int i = 0; i < function.ArgsCount; i++)
                functionExpressionArgumetns.Add(this.Nodes.Pop());
            functionExpressionArgumetns.Reverse();
            var callFunction = Expression.Call(Expression.Constant(method.FunctionDelegate.Target), method.FunctionMethod, functionExpressionArgumetns);

            var resultAsObjectExpression = Expression.Convert(callFunction, typeof(object));
            var result = Expression.Lambda<Func<object>>(resultAsObjectExpression).Compile()();
            From(node, result);
        }

        public void Visit(FromTableNode node)
        {
            if (_cte.ContainsKey(node.Table.TableOrView))
            {
                Visit(new InMemoryTableFromNode(node.Table.TableOrView, node.Alias));
                return;
            }

            var tableData = this._engine.DataManager.GeTable(node.Table.TableOrView, node.Table.Path).Result;

            var result = tableData.Results;
            var resultItemsType = tableData.ResultItemsType;
            var resultFields = tableData.ResultFields;

            Expression resultsAsQueryableExpression = Expression.Call(
                typeof(ParallelEnumerable),
                "AsParallel",
                new Type[] { resultItemsType },
                Expression.Constant(result));

            Expression.Constant(resultsAsQueryableExpression);

            Type resultItemType = expressionHelper.CreateAnonymousType(resultFields);
            var resultItemExpression = Expression.Parameter(resultItemsType, node.Table.ToString());

            List<MemberBinding> resultBindings = new List<MemberBinding>();
            int fieldIndex = 0;
            foreach (var field in resultFields)
            {
                if (resultItemsType == typeof(object[]))
                {
                    MemberBinding assignment = Expression.Bind(
                        resultItemType.GetField(field.Name),
                        Expression.Convert(Expression.ArrayAccess(resultItemExpression, Expression.Constant(fieldIndex)), field.FieldType)
                    );
                    resultBindings.Add(assignment);
                    fieldIndex++;
                }
                else
                {
                    //"SelectProp = rowOfDataSource.GetValue(..fieldName..)"
                    MemberBinding assignment = Expression.Bind(
                        resultItemType.GetField(field.Name),
                        Expression.PropertyOrField(resultItemExpression, field.Name)
                        );
                    resultBindings.Add(assignment);
                }
            }

            //"new AnonymousType()"
            var creationExpression = Expression.New(resultItemType.GetConstructor(Type.EmptyTypes));

            //"new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            var initialization = Expression.MemberInit(creationExpression, resultBindings);

            //"item => new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            Expression expression = Expression.Lambda(initialization, resultItemExpression, _state.QueryItemIndex);
            var call = Expression.Call(
                typeof(ParallelEnumerable),
                "Select",
                new Type[] { resultItemsType, resultItemType },
                resultsAsQueryableExpression,
                expression);

            Nodes.Push(call);
            
            //"AnonymousType input"
            this._state.QueryItem = Expression.Parameter(resultItemType, "item_" + resultItemType.Name);
            this._state.Alias2QueryItem[node.Alias] = this._state.QueryItem;

            //"IQueryable<AnonymousType> input"
            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(resultItemType), "query");
        }

        public void From(FromNode node, object result)
        {
            var resultType = result.GetType();
            var resultItemsType = result.GetType().GetElementType();
            Expression resultsAsQueryableExpression = null;

            List<(string Name, Type FieldType)> resultFields = null;
            if (resultItemsType != null)
            {
                resultFields = resultItemsType.GetProperties().Select(x => (x.Name, x.PropertyType)).ToList();

                resultsAsQueryableExpression = Expression.Call(
                    typeof(ParallelEnumerable),
                    "AsParallel",
                    new Type[] { resultItemsType },
                    Expression.Constant(result));
            }
            else if (typeof(IAsyncDataReader).IsAssignableFrom(resultType))
            {
                var resultReader = (IAsyncDataReader)result;
                resultItemsType = typeof(object[]);
                resultFields = Enumerable
                    .Range(0, resultReader.FieldCount)
                    .Select(x => (resultReader.GetName(x), resultReader.GetFieldType(x)))
                    .ToList();

                resultsAsQueryableExpression = Expression.Call(
                    typeof(ParallelEnumerable),
                    "AsParallel",
                    new Type[] { resultItemsType },
                    Expression.Constant(new AsyncDataReaderEnumerable(resultReader, this._cancellationToken)));
                //Expression.Constant(AsEnumerable(resultReader)));

            }
            else if (typeof(IDataReader).IsAssignableFrom(resultType))
            {
                var resultReader = (IDataReader)result;
                resultItemsType = typeof(object[]);
                resultFields = Enumerable
                    .Range(0, resultReader.FieldCount)
                    .Select(x => (resultReader.GetName(x), resultReader.GetFieldType(x)))
                    .ToList();

                resultsAsQueryableExpression = Expression.Call(
                    typeof(ParallelEnumerable),
                    "AsParallel",
                    new Type[] { resultItemsType },
                    Expression.Constant(new DataReaderEnumerable(resultReader, this._cancellationToken)));
                //Expression.Constant(AsEnumerable(resultReader)));

            }


            Type outputItemType = expressionHelper.CreateAnonymousType(resultFields);
            var resultItemExpression = Expression.Parameter(resultItemsType, "entityItem");

            List<MemberBinding> bindings = new List<MemberBinding>();
            int fieldIndex = 0;
            foreach (var field in resultFields)
            {
                if (resultItemsType == typeof(object[]))
                {
                    MemberBinding assignment = Expression.Bind(
                        outputItemType.GetField(field.Name),
                        Expression.Convert(Expression.ArrayAccess(resultItemExpression, Expression.Constant(fieldIndex)), field.FieldType)
                    );
                    bindings.Add(assignment);
                    fieldIndex++;
                }
                else
                {
                    //"SelectProp = rowOfDataSource.GetValue(..fieldName..)"
                    MemberBinding assignment = Expression.Bind(
                        outputItemType.GetField(field.Name),
                        Expression.PropertyOrField(resultItemExpression, field.Name)
                        );
                    bindings.Add(assignment);
                }
            }

            //"new AnonymousType()"
            var creationExpression = Expression.New(outputItemType.GetConstructor(Type.EmptyTypes));

            //"new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            var initialization = Expression.MemberInit(creationExpression, bindings);

            //"item => new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            Expression expression = Expression.Lambda(initialization, resultItemExpression, _state.QueryItemIndex);

            var call = Expression.Call(
                typeof(ParallelEnumerable),
                "Select",
                new Type[] { resultItemsType, outputItemType },
                resultsAsQueryableExpression,
                expression);

            Nodes.Push(call);

            //"AnonymousType input"
            this._state.QueryItem = Expression.Parameter(outputItemType, "item_" + outputItemType.Name);
            this._state.Alias2QueryItem[node.Alias] = this._state.QueryItem;

            //"IQueryable<AnonymousType> input"
            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(outputItemType), "query");
        }

        public void Visit(InMemoryTableFromNode node)
        {
            var table = _cte[node.VariableName];

            //Get from IQueryable<AnonymousType>
            var outputitemType = table.Type.GetGenericArguments()[0];

            //"AnonymousType input"
            this._state.QueryItem = Expression.Parameter(outputitemType, "item_" + outputitemType.Name);
            this._state.Alias2QueryItem[node.Alias] = this._state.QueryItem;

            //"IQueryable<AnonymousType> input"
            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(outputitemType), "query");

            Nodes.Push(table);
        }

        public void Visit(ExpressionFromNode node)
        {
        }

        public void Visit(IntoNode node)
        {
        }

        public void Visit(QueryScope node)
        {
        }

        public void Visit(QueryNode node)
        {
            Expression top = node?.Select.Top != null ? Nodes.Pop() : null;

            Expression select = node.Select != null ? Nodes.Pop() : null;

            Expression orderBy = node.OrderBy != null ? Nodes.Pop() : null;

            Expression take = node.Take != null ? Nodes.Pop() : null;
            Expression skip = node.Skip != null ? Nodes.Pop() : null;

            Expression having = node.GroupBy?.Having != null ? Nodes.Pop() : null;

            Expression groupBy = node.GroupBy != null ? Nodes.Pop() : null;

            Expression where = node.Where != null ? Nodes.Pop() : null;

            Expression from = node.From != null ? Nodes.Pop() : null;


            Expression last = from;

            if (where != null)
            {
                last = Expression.Invoke(where, last);
            }

            if (groupBy != null)
            {
                last = Expression.Invoke(groupBy, last);
            }

            if (having != null)
            {
                last = Expression.Invoke(having, last);
            }

            if (skip != null)
            {
                last = Expression.Invoke(skip, last);
            }

            if (take != null)
            {
                last = Expression.Invoke(take, last);
            }

            if (orderBy != null)
            {
                last = Expression.Invoke(orderBy, last);
            }

            if (select != null)
            {
                if (last != null)
                {
                    last = Expression.Invoke(select, last);
                }
                else
                {
                    last = Expression.Invoke(select);
                }
            }

            if (top != null)
            {
                last = Expression.Invoke(top, last);
            }

            Nodes.Push(last);
        }

        public void Visit(RootNode node)
        {
            if (Nodes.Any())
            {
                Expression last = Nodes.Pop();
                last = last.WithCancellation(this._cancellationToken);
                Expression<Func<object>> toStream = Expression.Lambda<Func<object>>(last);
                var compiledToStream = toStream.Compile();
                Result = compiledToStream();
            }
        }

        public void Visit(SingleSetNode node)
        {
            throw new NotImplementedException();
        }

        public void Visit(UnionNode node)
        {
            var secondSequence = Nodes.Pop();
            var firstSequence = Nodes.Pop();

            var outputItemType = expressionHelper.CreateAnonymousTypeSameAs(firstSequence.GetItemType());
            firstSequence = firstSequence.SelectAs(outputItemType);
            secondSequence = secondSequence.SelectAs(outputItemType);

            Expression comparer = null;
            if (node.Keys.Length > 0)
            {
                var comparerType = expressionHelper.CreateEqualityComparerForType(outputItemType, node.Keys);
                comparer = Expression.New(comparerType);
            }

            var call = firstSequence.Union(secondSequence, comparer);

            Nodes.Push(call);

        }

        public void Visit(UnionAllNode node)
        {
            var secondSequence = Nodes.Pop();
            var firstSequence = Nodes.Pop();

            var outputItemType = expressionHelper.CreateAnonymousTypeSameAs(firstSequence.GetItemType());
            firstSequence = firstSequence.SelectAs(outputItemType);
            secondSequence = secondSequence.SelectAs(outputItemType);

            var call = firstSequence.Concat(secondSequence);

            Nodes.Push(call);
        }

        public void Visit(ExceptNode node)
        {
            var secondSequence = Nodes.Pop();
            var firstSequence = Nodes.Pop();

            var outputItemType = expressionHelper.CreateAnonymousTypeSameAs(firstSequence.GetItemType());
            firstSequence = firstSequence.SelectAs(outputItemType);
            secondSequence = secondSequence.SelectAs(outputItemType);

            Expression comparer = null;
            if (node.Keys.Length > 0)
            {
                var comparerType = expressionHelper.CreateEqualityComparerForType(outputItemType, node.Keys);
                comparer = Expression.New(comparerType);
            }

            var call = firstSequence.Except(secondSequence, comparer);

            Nodes.Push(call);
        }

        public void Visit(IntersectNode node)
        {
            var secondSequence = Nodes.Pop();
            var firstSequence = Nodes.Pop();

            var outputItemType = expressionHelper.CreateAnonymousTypeSameAs(firstSequence.GetItemType());
            firstSequence = firstSequence.SelectAs(outputItemType);
            secondSequence = secondSequence.SelectAs(outputItemType);

            var call = firstSequence.Intersect(secondSequence);

            Nodes.Push(call);
        }

        public void Visit(PutTrueNode node)
        {
            throw new NotImplementedException();
        }

        public void Visit(MultiStatementNode node)
        {
        }

        public void Visit(StatementsArrayNode node)
        {

        }

        public void Visit(StatementNode node)
        {

        }

        public void Visit(CteExpressionNode node)
        {
        }

        public void Visit(CteInnerExpressionNode node)
        {
            _cte[node.Name] = Nodes.Pop();
        }

        public void Visit(JoinFromNode node)
        {
            if (node.JoinType == JoinType.Inner)
                VisitInnerJoin(node);
            if (node.JoinType == JoinType.OuterLeft)
                VisitLeftJoin(node);
        }

        public void VisitLeftJoin(JoinFromNode node)
        {
            //new List<JoinFromNode>().AsParallel<JoinFromNode>()
            //    .GroupJoin(new List<CteExpressionNode>().AsParallel<CteExpressionNode>(), x => x.Id, x => x.Id, (x, y) => new { x = x, y = y })
            //    .SelectMany(x => x.y.Select())


            var onNode = ((EqualityNode)node.Expression);


            var secondSequenceKeyExpression = this.Nodes.Pop();
            var firstSequenceKeyExpression = this.Nodes.Pop();

            var secondSequence = this.Nodes.Pop();
            var secondSequenceAlias = node.With.Alias;
            var secondSequenceKeyLambda = Expression.Lambda(secondSequenceKeyExpression, (ParameterExpression)this._state.Alias2QueryItem[secondSequenceAlias]);

            var firstSequence = this.Nodes.Pop();
            var firstSequenceAlias = node.Source.Alias;
            var firstSequenceKeyLambda = Expression.Lambda(firstSequenceKeyExpression, (ParameterExpression)this._state.Alias2QueryItem[firstSequenceAlias]);

            bool isFirstJoin = (node.Source is JoinFromNode) == false;

            var groupJoin = firstSequence.GroupJoin(
                secondSequence,
                firstSequenceKeyLambda,
                secondSequenceKeyLambda,
                //FirstSequenceItemType firstItem, IEnumerable<SecondSequenceItemType> secondItemsList
                (firstItem, secondItemsList) =>
                {
                    var returnType = this.expressionHelper.CreateAnonymousType(new (string Alias, Type Type)[]
                    {
                        (firstSequenceAlias, firstItem.Type),
                        (secondSequenceAlias, secondItemsList.Type)
                    });

                    var newItem = Expression.MemberInit(
                        Expression.New(returnType.GetConstructor(Type.EmptyTypes)),
                        new List<MemberBinding>()
                        {
                            Expression.Bind(returnType.GetField(firstSequenceAlias), firstItem),
                            Expression.Bind(returnType.GetField(secondSequenceAlias), secondItemsList.DefaultIfEmpty())
                        });

                    return newItem;
                });


 
            //<FirstSequenceItemType, IEnumerable<SecondSeqequenceItemType>> selectManyItemParameter
            var selectManyMethodsCall = groupJoin.SelectMany((groupItem) =>
            {
                var firstItem = Expression.PropertyOrField(groupItem, firstSequenceAlias);
                var selectMethodsCall = groupItem.PropertyOrField(secondSequenceAlias)
                    .Select((secondItem) =>
                    {
                        //SecondSeqequenceItemType inTheGroup
                        
                        List<(string Alias, Type Type)> returnFields = new List<(string Alias, Type Type)>();
                        List<(string Alias, Expression Value)> returnFieldsBindings = new List<(string Alias, Expression Value)>();


                        if (isFirstJoin)
                        {
                            returnFields.Add((firstSequenceAlias, firstSequence.GetItemType()));
                            returnFieldsBindings.Add((firstSequenceAlias, firstItem));
                        }
                        else
                        {
                            foreach(var field in firstSequence.GetItemType().GetFields())
                            {
                                returnFields.Add((field.Name, field.FieldType));
                                returnFieldsBindings.Add((field.Name, Expression.PropertyOrField(firstItem, field.Name)));
                            }
                        }
                        returnFields.Add((secondSequenceAlias, secondSequence.GetItemType()));
                        returnFieldsBindings.Add((secondSequenceAlias, secondItem));

                        var returnType = this.expressionHelper.CreateAnonymousType(returnFields.ToArray());
                        List<MemberBinding> resultBindings = new List<MemberBinding>();
                        //"SelectProp = inputItem.Prop"
                        foreach (var binding in returnFieldsBindings)
                        {
                            resultBindings.Add(Expression.Bind(returnType.GetField(binding.Alias), binding.Value));
                        }
                        var createResultInstance = Expression.MemberInit(
                            Expression.New(returnType.GetConstructor(Type.EmptyTypes)),
                            resultBindings);

                        return createResultInstance;


                    });

                return selectMethodsCall;

            });

            Nodes.Push(selectManyMethodsCall);

            var resultItemType = selectManyMethodsCall.GetItemType();
            this._state.QueryItem = Expression.Parameter(resultItemType, "item_" + resultItemType.Name);
            this._state.Alias2QueryItem[node.Alias] = this._state.QueryItem;

            foreach (var field in resultItemType.GetFields())
                this._state.Alias2QueryItem[field.Name] = Expression.PropertyOrField(this._state.QueryItem, field.Name);
            //this._queryState.Alias2Item[join.Alias] = Expression.PropertyOrField(this._queryState.Item, fromItemAlias);


            this._state.Query = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(resultItemType), "query");


        }

        public void VisitInnerJoin(JoinFromNode node)
        {
            var onNode = ((EqualityNode)node.Expression);

            var secondSequenceKeyExpression = this.Nodes.Pop();
            var firstSequenceKeyExpression = this.Nodes.Pop();

            var secondSequence = this.Nodes.Pop();
            var secondSequenceAlias = node.With.Alias;
            var secondSequenceKeyLambda = Expression.Lambda(secondSequenceKeyExpression, (ParameterExpression)this._state.Alias2QueryItem[secondSequenceAlias]);
            
            var firstSequence = this.Nodes.Pop();
            var firstSequenceAlias = node.Source.Alias;
            var firstSequenceKeyLambda = Expression.Lambda(firstSequenceKeyExpression, (ParameterExpression)this._state.Alias2QueryItem[firstSequenceAlias]);


            bool isFirstJoin = (node.Source is JoinFromNode) == false;

            var join = firstSequence.Join(
                secondSequence,
                firstSequenceKeyLambda,
                secondSequenceKeyLambda,
                (firstItem, secondItem) =>
                {
                    List<(string Alias, Type Type)> returnFields = new List<(string Alias, Type Type)>();
                    List<(string Alias, Expression Value)> returnFieldsBindings = new List<(string Alias, Expression Value)>();


                    if (isFirstJoin)
                    {
                        returnFields.Add((firstSequenceAlias, firstSequence.GetItemType()));
                        returnFieldsBindings.Add((firstSequenceAlias, firstItem));
                    }
                    else
                    {
                        foreach (var field in firstSequence.GetItemType().GetFields())
                        {
                            returnFields.Add((field.Name, field.FieldType));
                            returnFieldsBindings.Add((field.Name, Expression.PropertyOrField(firstItem, field.Name)));
                        }
                    }
                    returnFields.Add((secondSequenceAlias, secondSequence.GetItemType()));
                    returnFieldsBindings.Add((secondSequenceAlias, secondItem));

                    var returnType = this.expressionHelper.CreateAnonymousType(returnFields.ToArray());
                    List<MemberBinding> resultBindings = new List<MemberBinding>();
                    //"SelectProp = inputItem.Prop"
                    foreach (var binding in returnFieldsBindings)
                    {
                        resultBindings.Add(Expression.Bind(returnType.GetField(binding.Alias), binding.Value));
                    }
                    var createResultInstance = Expression.MemberInit(
                        Expression.New(returnType.GetConstructor(Type.EmptyTypes)),
                        resultBindings);

                    return createResultInstance;
                }
            );

            Nodes.Push(join);
            //countries.Country, .City
            //"AnonymousType input"
            this._state.QueryItem = Expression.Parameter(join.GetItemType(), "item_" + join.GetItemType().Name);
            this._state.Alias2QueryItem[node.Alias] = this._state.QueryItem;

            foreach (var field in join.GetItemType().GetFields())
                this._state.Alias2QueryItem[field.Name] = Expression.PropertyOrField(this._state.QueryItem, field.Name);
            //this._queryState.Alias2Item[join.Alias] = Expression.PropertyOrField(this._queryState.Item, fromItemAlias);


            this._state.Query = Expression.Parameter(join.Type, "query");
        }

        public void Visit(JoinsNode node)
        {
            //var joinNodes = new List<(
            //    JoinFromNode JoinNode,
            //    Expression OnExpression,
            //    Expression JoinExpression,
            //    Type ItemType,
            //    string ItemAlias)>();
            //FromNode fromNode = null;
            //Expression fromExpression = null;
            //Type fromItemType = null;
            //string fromItemAlias = null;
            //JoinFromNode joinNode = node.Joins;
            //do
            //{
            //    var onExpression = Nodes.Pop();
            //    var joinExpression = Nodes.Pop();
            //    var itemType = this.expressionHelper.GetItemType(joinExpression);
            //    var itemAlias = joinNode.With.Alias;
            //    joinNodes.Add((joinNode, onExpression, joinExpression, itemType, itemAlias));
            //    if (joinNode.Source is JoinFromNode)
            //    {
            //        joinNode = joinNode.Source as JoinFromNode;
            //    }
            //    else
            //    {
            //        fromNode = joinNode.Source;
            //        fromExpression = Nodes.Pop();
            //        fromItemType = this.expressionHelper.GetItemType(fromExpression);
            //        fromItemAlias = fromNode.Alias;
            //        joinNode = null;
            //    }
            //} while (joinNode != null);


            //var ouputTypeFields = new List<(string Alias, Type Type)>();
            //foreach (var join in joinNodes)
            //    ouputTypeFields.Add((join.ItemAlias, join.ItemType));
            //ouputTypeFields.Add((fromItemAlias, fromItemType));

            //var outputItemType = this.expressionHelper.CreateAnonymousType(ouputTypeFields.ToArray());

            //List<MemberBinding> bindings = new List<MemberBinding>();
            ////"SelectProp = inputItem.Prop"
            //foreach (var field in ouputTypeFields)
            //{
            //    bindings.Add(Expression.Bind(
            //        outputItemType.GetField(field.Alias),
            //        this._queryState.Alias2Item[field.Alias]));
            //}

            ////"new AnonymousType()"
            //var creationExpression = Expression.New(outputItemType.GetConstructor(Type.EmptyTypes));

            ////"new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            //var initialization = Expression.MemberInit(creationExpression, bindings);

            ////"item => new AnonymousType() { SelectProp = item.name, SelectProp2 = item.SelectProp2) }"
            //Expression expression = Expression.Lambda(initialization, (ParameterExpression)this._queryState.Alias2Item[ouputTypeFields.FirstOrDefault().Alias]);

            //Expression lastJoinExpression = null;
            //Type lastJoinItemType = null;
            //string LastJoinItemAlias = null;
            //for (int i = 0; i < joinNodes.Count; i++)
            //{
            //    var join = joinNodes[i];
            //    if (i == 0)
            //    {
            //        var onCall = Expression.Call(
            //            typeof(ParallelEnumerable),
            //            "Where",
            //            new Type[] { join.ItemType },
            //            join.JoinExpression,
            //            Expression.Lambda(join.OnExpression, (ParameterExpression)this._queryState.Alias2Item[join.ItemAlias]));

            //        if (join.JoinNode.JoinType == JoinType.OuterLeft)
            //        {
            //            onCall = Expression.Call(
            //                typeof(ParallelEnumerable),
            //                "DefaultIfEmpty",
            //                new Type[] { join.ItemType },
            //                onCall,
            //                Expression.Constant(null, join.ItemType));
            //        }

            //        lastJoinExpression = Expression.Call(
            //            typeof(ParallelEnumerable),
            //            "Select",
            //            new Type[] { join.ItemType, outputItemType },
            //            onCall,
            //            expression);



            //        lastJoinItemType = join.ItemType;
            //        LastJoinItemAlias = join.ItemAlias;
            //    }
            //    else
            //    {
            //        var onCall = Expression.Call(
            //            typeof(ParallelEnumerable),
            //            "Where",
            //            new Type[] { join.ItemType },
            //            join.JoinExpression,
            //            Expression.Lambda(join.OnExpression, (ParameterExpression)this._queryState.Alias2Item[join.ItemAlias]));

            //        if (join.JoinNode.JoinType == JoinType.OuterLeft)
            //        {
            //            onCall = Expression.Call(
            //                typeof(ParallelEnumerable),
            //                "DefaultIfEmpty",
            //                new Type[] { join.ItemType },
            //                onCall,
            //                Expression.Constant(null, join.ItemType));
            //        }

            //        var selectLambda = Expression.Lambda(
            //            Expression.Convert(lastJoinExpression, typeof(IEnumerable<>).MakeGenericType(outputItemType)),
            //            (ParameterExpression)this._queryState.Alias2Item[join.ItemAlias]);
            //        lastJoinExpression = Expression.Call(
            //            typeof(ParallelEnumerable),
            //            "SelectMany",
            //            new Type[] { join.ItemType, outputItemType },
            //            onCall,
            //            selectLambda);
            //    }
            //}


            //var fromLambda = Expression.Lambda(
            //        Expression.Convert(lastJoinExpression, typeof(IEnumerable<>).MakeGenericType(outputItemType)),
            //        (ParameterExpression)this._queryState.Alias2Item[fromNode.Alias]);
            //var fromCall = Expression.Call(
            //    typeof(ParallelEnumerable),
            //    "SelectMany",
            //    new Type[] { fromItemType, outputItemType },
            //    fromExpression,
            //    fromLambda
            //    );

            //Nodes.Push(fromCall);

            ////"AnonymousType input"
            //this._queryState.Item = Expression.Parameter(outputItemType, "item_" + outputItemType.Name);
            //this._queryState.Alias2Item[node.Alias] = this._queryState.Item;

            //foreach (var join in joinNodes)
            //    this._queryState.Alias2Item[join.ItemAlias] = Expression.PropertyOrField(this._queryState.Item, join.ItemAlias);
            //this._queryState.Alias2Item[fromItemAlias] = Expression.PropertyOrField(this._queryState.Item, fromItemAlias);


            //this._queryState.Input = Expression.Parameter(typeof(ParallelQuery<>).MakeGenericType(outputItemType), "input");
        }

        public void Visit(JoinNode node)
        {
            throw new NotImplementedException();
        }

        public void Visit(OrderByNode node)
        {
            var fieldNodes = new Expression[node.Fields.Length];
            for (var i = 0; i < node.Fields.Length; i++)
                fieldNodes[node.Fields.Length - 1 - i] = Nodes.Pop();

            Expression lastCall = null;
            for (int i = 0; i < fieldNodes.Length; i++)
            {
                var fieldNode = node.Fields[i];
                var field = fieldNodes[i];
                if (i == 0)
                {
                    lastCall = Expression.Call(
                       typeof(ParallelEnumerable),
                       fieldNode.Order == Order.Ascending ? "OrderBy" : "OrderByDescending",
                       new Type[] { this._state.QueryItem.Type, field.Type },
                       _state.Query,
                       Expression.Lambda(field, new[] { _state.QueryItem }));
                }
                else
                {
                    lastCall = Expression.Call(
                        typeof(ParallelEnumerable),
                        fieldNode.Order == Order.Ascending ? "ThenBy" : "ThenByDescending",
                        new Type[] { this._state.QueryItem.Type, field.Type },
                        lastCall,
                        Expression.Lambda(field, new[] { _state.QueryItem }));
                }
            }

            var orderBy = Expression.Lambda(
                lastCall,
                //node.ToString(),
                new[] { this._state.Query });
            Nodes.Push(orderBy);
        }

        public void Visit(CreateTableNode node)
        {
            throw new NotImplementedException();
        }

        public void Visit(CaseNode node)
        {
            var returnLabel = Expression.Label(node.ReturnType, "return");

            // when then
            List<Expression> statements = new List<Expression>();
            for (int i = 0; i < node.WhenThenPairs.Length; i++)
            {
                Expression then = Nodes.Pop();
                Expression when = Nodes.Pop();
                statements.Add(Expression.IfThen(
                    when,
                    Expression.Return(returnLabel, then)));
            }

            // else
            Expression elseThen = Nodes.Pop();
            statements.Add(Expression.Return(returnLabel, elseThen));

            // return value
            statements.Add(Expression.Label(returnLabel, Expression.Default(node.ReturnType)));

            var caseStatement = Expression.Invoke(Expression.Lambda(Expression.Block(statements)));

            Nodes.Push(caseStatement);

            //ParameterExpression resultResult = Expression.Parameter(node.ReturnType, "result");
            //List<(Expression Then, Expression When)> whenThenPairs = new List<(Expression Then, Expression When)>();
            //for (int i = 0; i < node.WhenThenPairs.Length; i++)
            //{
            //    (Expression Then, Expression When) whenThenPair = (Nodes.Pop(), Nodes.Pop());
            //    whenThenPairs.Add(whenThenPair);
            //}
            //Expression elseThen = Nodes.Pop();

            //Expression last = elseThen;
            //for (int i = whenThenPairs.Count - 1; i >= 0; i -= 1)
            //{
            //    last = Expression.IfThenElse(
            //        whenThenPairs[i].When,
            //        Expression.Return(returnLabel, whenThenPairs[i].Then),
            //        Expression.Return(returnLabel, last));
            //}

            //Nodes.Push(Expression.Block(last, Expression.Constant(1)));
        }

        public void Visit(TypeNode node)
        {
            Nodes.Push(Expression.Constant(node.ReturnType));
        }

        public void Visit(ExecuteNode node)
        {
            Expression valueExpression = Nodes.Pop();
            var valueAsObjectExpression = Expression.Convert(valueExpression, typeof(object));
            var value = Expression.Lambda<Func<object>>(valueAsObjectExpression).Compile()();
            if (node.VariableToSet != null)
            {
                _engine.SetVariable(node.VariableToSet.Name, value);
            }
        }

        public void SetScope(Scope scope)
        {
            // if (scope?.Name == "Query")
            //     _queryState.Input = null;
        }

        public void SetQueryIdentifier(string identifier)
        {

        }
    }

}