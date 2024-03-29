﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace Zhucai.TextToDelegate
{
    /// <summary>
    /// Lambda表达式的解析器核心类
    /// </summary>
    /// <typeparam name="TDelegate"></typeparam>
    internal class ExpressionParserCore<TDelegate>
    {
        #region fields.字段

        private CodeParser _codeParser;

        private Type _delegateType;

        private Type _defaultInstanceType;
        private ParameterExpression _defaultInstanceParam;

        private Type[] _paramTypes;

        private bool _firstTypeIsDefaultInstance;

        /// <summary>
        /// 存放参数
        /// </summary>
        private List<ParameterExpression> _params = new List<ParameterExpression>();

        /// <summary>
        /// 存放操作符的优先级
        /// </summary>
        static private Dictionary<string, int> _operatorPriorityLevel = new Dictionary<string, int>();

        /// <summary>
        /// 存放数字类型的隐式转换级别
        /// </summary>
        static private Dictionary<Type, int> _numberTypeLevel = new Dictionary<Type, int>();

        #endregion


        #region properties.属性

        /// <summary>
        /// 引入的命名空间集。
        /// </summary>
        public List<string> Namespaces { get; private set; }

        #endregion


        #region ctor.构造函数

        static ExpressionParserCore()
        {
            // 初始化_operatorPriorityLevel
            _operatorPriorityLevel.Add("(", 100);
            _operatorPriorityLevel.Add(")", 100);
            _operatorPriorityLevel.Add("[", 100);
            _operatorPriorityLevel.Add("]", 100);

            _operatorPriorityLevel.Add(".", 13);
            _operatorPriorityLevel.Add("function()", 13);
            _operatorPriorityLevel.Add("index[]", 13);
            _operatorPriorityLevel.Add("++behind", 13);
            _operatorPriorityLevel.Add("--behind", 13);
            _operatorPriorityLevel.Add("new", 13);
            _operatorPriorityLevel.Add("typeof", 13);
            _operatorPriorityLevel.Add("checked", 13);
            _operatorPriorityLevel.Add("unchecked", 13);
            _operatorPriorityLevel.Add("->", 13);

            _operatorPriorityLevel.Add("++before", 12);
            _operatorPriorityLevel.Add("--before", 12);
            _operatorPriorityLevel.Add("+before", 12);
            _operatorPriorityLevel.Add("-before", 12);
            _operatorPriorityLevel.Add("!", 12);
            _operatorPriorityLevel.Add("~", 12);
            _operatorPriorityLevel.Add("convert()", 12);
            _operatorPriorityLevel.Add("sizeof", 12);

            _operatorPriorityLevel.Add("*", 11);
            _operatorPriorityLevel.Add("/", 11);
            _operatorPriorityLevel.Add("%", 11);
            _operatorPriorityLevel.Add("+", 10);
            _operatorPriorityLevel.Add("-", 10);
            _operatorPriorityLevel.Add("<<", 9);
            _operatorPriorityLevel.Add(">>", 9);
            _operatorPriorityLevel.Add(">", 8);
            _operatorPriorityLevel.Add("<", 8);
            _operatorPriorityLevel.Add(">=", 8);
            _operatorPriorityLevel.Add("<=", 8);
            _operatorPriorityLevel.Add("is", 8);
            _operatorPriorityLevel.Add("as", 8);
            _operatorPriorityLevel.Add("==", 7);
            _operatorPriorityLevel.Add("!=", 7);
            _operatorPriorityLevel.Add("&", 6);
            _operatorPriorityLevel.Add("^", 6);
            _operatorPriorityLevel.Add("|", 6);
            _operatorPriorityLevel.Add("&&", 5);
            _operatorPriorityLevel.Add("||", 5);
            _operatorPriorityLevel.Add("?", 5);
            _operatorPriorityLevel.Add("??", 4);
            _operatorPriorityLevel.Add("=", 4);
            _operatorPriorityLevel.Add("+=", 4);
            _operatorPriorityLevel.Add("-=", 4);
            _operatorPriorityLevel.Add("*=", 4);
            _operatorPriorityLevel.Add("/=", 4);
            _operatorPriorityLevel.Add("%=", 4);
            _operatorPriorityLevel.Add("&=", 4);
            _operatorPriorityLevel.Add("|=", 4);
            _operatorPriorityLevel.Add("^=", 4);
            _operatorPriorityLevel.Add(">>=", 4);
            _operatorPriorityLevel.Add("<<=", 4);

            // 初始化_numberTypeLevel
            _numberTypeLevel.Add(typeof(byte), 1);
            _numberTypeLevel.Add(typeof(short), 2);
            _numberTypeLevel.Add(typeof(ushort), 3);
            _numberTypeLevel.Add(typeof(int), 4);
            _numberTypeLevel.Add(typeof(uint), 5);
            _numberTypeLevel.Add(typeof(long), 6);
            _numberTypeLevel.Add(typeof(ulong), 7);
            _numberTypeLevel.Add(typeof(float), 8);
            _numberTypeLevel.Add(typeof(double), 9);
            _numberTypeLevel.Add(typeof(decimal), 10);
        }

        /// <summary>
        /// 构造Lambda表达式的解析器
        /// </summary>
        /// <param name="code">lambda表达式代码。如：m=>m.ToString()</param>
        internal ExpressionParserCore(Type delegateType,string code,  Type defaultInstanceType, Type[] paramTypes, bool firstTypeIsDefaultInstance)
        {
            if (code == null)
            {
                throw new ArgumentNullException("code");
            }

            this._codeParser = new CodeParser(code);
            this._defaultInstanceType = defaultInstanceType;
            this._firstTypeIsDefaultInstance = firstTypeIsDefaultInstance;
            this._paramTypes = paramTypes;
            this.Namespaces = new List<string>();
            if (delegateType != null)
            {
                this._delegateType = delegateType;
            }
            else
            {
                this._delegateType = typeof(TDelegate);
            }

            // 判断是否有指定具体的委托类型
            if (firstTypeIsDefaultInstance && this._delegateType.IsSubclassOf(typeof(MulticastDelegate)))
            {
                MethodInfo methodInfo = _delegateType.GetMethod("Invoke");
                if (methodInfo != null)
                {
                    ParameterInfo firstParam = methodInfo.GetParameters().FirstOrDefault();
                    if (firstParam != null)
                    {
                        this._defaultInstanceType = firstParam.ParameterType;
                    }
                }
            }

            if (this._defaultInstanceType != null)
            {
                _defaultInstanceParam = Expression.Parameter(this._defaultInstanceType, "___DefaultInstanceParam");

                // 添加默认参数
                this._params.Insert(0, this._defaultInstanceParam);
            }
        }

        #endregion


        #region method.方法

        /// <summary>
        /// 转换成LambdaExpression
        /// </summary>
        /// <returns></returns>
        public LambdaExpression ToLambdaExpression()
        {
            // 获取委托的参数类型
            if (this._paramTypes == null)
            {
                MethodInfo methodInfo = _delegateType.GetMethod("Invoke");
                if (methodInfo != null)
                {
                    this._paramTypes = methodInfo.GetParameters().Select(m => m.ParameterType).ToArray();
                }
            }

            int paramIndexPrefix = 0;
            if (_defaultInstanceType != null)
            {
                paramIndexPrefix = 1;
            }

            // 检查是否有lambda前置符(如:m=>)
            string val = _codeParser.ReadString();
            bool hasLambdaPre = false;
            if (val == "(")
            {
                string bracketContent = GetBracketString(true);
                if (bracketContent != null)
                {
                    string lambdaOperator = _codeParser.ReadString();
                    if (lambdaOperator == "=>")
                    {
                        hasLambdaPre = true;

                        // 解析参数
                        string[] paramsName = bracketContent.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < paramsName.Length; i++)
                        {
                            string[] typeName = paramsName[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            Type paramType;
                            string paramName;
                            if (typeName.Length == 1)
                            {
                                paramType = this._paramTypes != null ? this._paramTypes[i + paramIndexPrefix] : typeof(object);
                                paramName = paramsName[i];
                            }
                            else
                            {
                                paramType = GetType(typeName[0]);
                                if (paramType == null)
                                {
                                    throw new ParseUnfindTypeException(typeName[0], this._codeParser.Index);
                                }
                                paramName = typeName[1];
                            }
                            this._params.Add(Expression.Parameter(paramType, paramName));
                        }
                    }
                }
            }
            else if (char.IsLetter(val[0]) || val[0] == '_')
            {
                // 解析参数
                string lambdaOperator = _codeParser.ReadString();
                if (lambdaOperator == "=>")
                {
                    hasLambdaPre = true;
                    this._params.Add(Expression.Parameter(this._paramTypes != null ? this._paramTypes[0 + paramIndexPrefix] : typeof(object), val));
                }
            }

            // 若没有lambda前置符(如:m=>)，则恢复_parser到初始状态
            if (!hasLambdaPre)
            {
                _codeParser.RevertPosition();
            }

            bool isCloseWrap;
            Expression expression = ReadExpression(0, null, out isCloseWrap);

            // 具体的Delegate
            if (this._delegateType.IsSubclassOf(typeof(MulticastDelegate)))
            {
                return Expression.Lambda(_delegateType, expression, this._params.ToArray());
            }
            // 不具体的Delegate
            else
            {
                return Expression.Lambda(expression, this._params.ToArray());
            }
        }

        /// <summary>
        /// 读取Expression。可能会引发递归。
        /// </summary>
        /// <param name="priorityLevel">当前操作的优先级</param>
        /// <param name="wrapStart">括号开始符(如果有)</param>
        /// <param name="isClosedWrap">是否遇到符号结束符</param>
        /// <returns></returns>
        private Expression ReadExpression(int priorityLevel, string wrapStart, out bool isClosedWrap)
        {
            // 初始化
            isClosedWrap = false;
            string val = this._codeParser.ReadString();
            if (val == null)
            {
                return null;
            }
            char firstChar = val[0];
            Expression currentExpression = null;

            /********************** (Start) 第一次读取，一元操作或一个对象 **************************/
            // 数字
            if (char.IsDigit(firstChar))
            {
                // 数字解析
                object constVal = ParseNumber(val);
                currentExpression = Expression.Constant(constVal);
            }
            // 非数字
            else
            {
                // 字母或字符
                switch (val)
                {
                    #region case "null":
                    case "null":
                        currentExpression = Expression.Constant(null);
                        break;
                    #endregion

                    #region case "true":
                    case "true":
                        currentExpression = Expression.Constant(true);
                        break;
                    #endregion

                    #region case "false":
                    case "false":
                        currentExpression = Expression.Constant(false);
                        break;
                    #endregion

                    //case "void":
                    //    currentExpression = Expression.Constant(typeof(System.Void));
                    //    break;

                    #region case "sizeof":
                    case "sizeof":
                        {
                            string str = GetBracketString(false);
                            Type type = GetType(str);
                            currentExpression = Expression.Constant(System.Runtime.InteropServices.Marshal.SizeOf(type));
                        }
                        break;
                    #endregion

                    #region case "typeof":
                    case "typeof":
                        {
                            //string str = GetBracketString(false);
                            _codeParser.ReadSymbol("(");
                            Type type = ReadType(null);
                            _codeParser.ReadSymbol(")");

                            currentExpression = Expression.Constant(type, typeof(Type));
                        }
                        break;
                    #endregion

                    #region case "new":
                    case "new":
                        {
                            // 获取类型
                            Type type = ReadType(_codeParser.ReadString());

                            // 是否数组
                            string bracketStart = _codeParser.ReadString();
                            if (bracketStart == "(")
                            {
                                // 获取参数
                                List<Expression> listParam = ReadParams("(", true);

                                // 获取构造函数
                                ConstructorInfo constructor = type.GetConstructor(listParam.ConvertAll<Type>(m => m.Type).ToArray());
                                currentExpression = Expression.New(constructor, listParam);

                                // 成员初始化/集合初始化
                                if (_codeParser.PeekString() == "{")
                                {
                                    _codeParser.ReadString();

                                    // 测试到底是:成员初始化or集合初始化
                                    var position = _codeParser.SavePosition();
                                    string str = _codeParser.ReadString();
                                    if (str != "}")
                                    {
                                        bool isMemberInit = (_codeParser.ReadString() == "=");
                                        _codeParser.RevertPosition(position);

                                        // 成员初始化
                                        if (isMemberInit)
                                        {
                                            List<MemberBinding> listMemberBinding = new List<MemberBinding>();
                                            string memberName;
                                            while ((memberName = _codeParser.ReadString()) != "}")
                                            {
                                                _codeParser.ReadSymbol("=");

                                                MemberInfo memberInfo = type.GetMember(memberName)[0];
                                                MemberBinding memberBinding = Expression.Bind(memberInfo, ReadExpression(0, wrapStart, out isClosedWrap));
                                                listMemberBinding.Add(memberBinding);

                                                // 逗号
                                                string comma = _codeParser.ReadString();
                                                if (comma == "}")
                                                {
                                                    break;
                                                }
                                                ParseException.Assert(comma, ",", _codeParser.Index);
                                            }
                                            currentExpression = Expression.MemberInit((NewExpression)currentExpression, listMemberBinding);
                                        }
                                        // 集合初始化
                                        else
                                        {
                                            List<Expression> listExpression = new List<Expression>();
                                            while (true)
                                            {
                                                listExpression.Add(ReadExpression(0, wrapStart, out isClosedWrap));

                                                // 逗号
                                                string comma = _codeParser.ReadString();
                                                if (comma == "}")
                                                {
                                                    break;
                                                }
                                                ParseException.Assert(comma, ",", _codeParser.Index);
                                            }
                                            currentExpression = Expression.ListInit((NewExpression)currentExpression, listExpression);
                                        }
                                    }
                                }
                            }
                            else if (bracketStart == "[")
                            {
                                string nextStr = _codeParser.PeekString();

                                // 读[]里的长度
                                List<Expression> listLen = new List<Expression>();
                                if (nextStr == "]")
                                {
                                    _codeParser.ReadString();
                                }
                                else
                                {
                                    listLen = ReadParams("[", true);
                                }

                                // 读{}里的数组初始化
                                string start = _codeParser.PeekString();
                                if (start == "{")
                                {
                                    List<Expression> listParams = ReadParams("{", false);
                                    currentExpression = Expression.NewArrayInit(type, listParams);
                                }
                                else
                                {
                                    currentExpression = Expression.NewArrayBounds(type, listLen);
                                }
                            }
                            else
                            {
                                throw new ParseUnknownException(bracketStart, _codeParser.Index);
                            }
                        }
                        break;
                    #endregion

                    #region case "+":
                    case "+":
                        // 忽略前置+
                        return ReadExpression(priorityLevel, wrapStart, out isClosedWrap);
                    #endregion

                    #region case "-":
                    case "-":
                        currentExpression = Expression.Negate(ReadExpression(GetOperatorLevel(val, true), wrapStart, out isClosedWrap));
                        break;
                    #endregion

                    #region case "!":
                    case "!":
                        currentExpression = Expression.Not(ReadExpression(GetOperatorLevel(val, true), wrapStart, out isClosedWrap));
                        break;
                    #endregion

                    #region case "~":
                    case "~":
                        currentExpression = Expression.Not(ReadExpression(GetOperatorLevel(val, true), wrapStart, out isClosedWrap));
                        break;
                    #endregion

                    #region case "(":
                    case "(":
                        {
                            CodeParserPosition position = _codeParser.SavePosition();
                            string str = GetBracketString(true);
                            Type type = GetType(str);

                            // 找到类型，作为类型转换处理
                            if (type != null)
                            {
                                currentExpression = Expression.Convert(ReadExpression(GetOperatorLevel("convert()", true), wrapStart, out isClosedWrap), type);
                            }
                            // 未找到类型，作为仅用来优先处理
                            else
                            {
                                _codeParser.RevertPosition(position);

                                // 分配一个新的isClosedWrap变量
                                bool newIsClosedWrap;
                                currentExpression = ReadExpression(0, val, out newIsClosedWrap);
                            }
                        }
                        break;
                    #endregion

                    #region case ")":
                    case ")":
                        {
                            // 结束一个isClosedWrap变量
                            isClosedWrap = true;
                            return null;
                        }
                    #endregion

                    #region case "]":
                    case "]":
                        {
                            // 结束一个isClosedWrap变量
                            isClosedWrap = true;
                            return null;
                        }
                    #endregion

                    #region case "}":
                    case "}":
                        {
                            // 结束一个isClosedWrap变量
                            isClosedWrap = true;
                            return null;
                        }
                    #endregion

                    #region case ".":
                    case ".":
                        {
                            //todo:?
                            //return null;
                            throw new ParseUnknownException(".", this._codeParser.Index);
                        }
                    #endregion

                    #region case ",":
                    case ",":
                        {
                            return ReadExpression(priorityLevel, wrapStart, out isClosedWrap);
                        }
                    #endregion

                    #region default:
                    default:
                        {
                            // 头Char是字母或下划线
                            if (char.IsLetter(firstChar) || firstChar == '_' || firstChar == '$')
                            {
                                // 默认实例的方法调用
                                if (_defaultInstanceType != null && _codeParser.PeekString() == "(")
                                {
                                    // 获取参数
                                    List<Expression> listParam = ReadParams("(", false);

                                    MethodInfo methodInfo = _params[0].Type.GetMethod(val,
                                        listParam.ConvertAll<Type>(m => m.Type).ToArray());
                                    currentExpression = Expression.Call(_params[0], methodInfo, listParam.ToArray());
                                }
                                // 参数 or 类 or  默认实例的属性
                                else
                                {
                                    ParameterExpression parameter;
                                    // 参数
                                    if ((parameter = this._params.SingleOrDefault(m => m.Name == val))
                                        != null)
                                    {
                                        currentExpression = parameter;
                                    }
                                    // 默认实例的属性
                                    else if (this._defaultInstanceType != null &&
                                        (this._defaultInstanceType.GetProperty(val) != null
                                            || this._defaultInstanceType.GetField(val) != null))
                                    {
                                        currentExpression = Expression.PropertyOrField(_params[0], val);
                                    }
                                    // 类
                                    else
                                    {
                                        Type type = ReadType(val);

                                        _codeParser.ReadSymbol(".");
                                        string strMember = _codeParser.ReadString();
                                        string strOperator = _codeParser.PeekString();

                                        // 静态方法
                                        if (strOperator == "(")
                                        {
                                            // 获取参数
                                            List<Expression> listParam = ReadParams(strOperator, false);

                                            if (parameter != null)
                                            {
                                                MethodInfo methodInfo = parameter.Type.GetMethod(strMember,
                                                    listParam.ConvertAll<Type>(m => m.Type).ToArray());
                                                currentExpression = Expression.Call(parameter, methodInfo, listParam.ToArray());
                                            }
                                            else
                                            {
                                                MethodInfo methodInfo = type.GetMethod(strMember, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                                                    listParam.ConvertAll<Type>(m => m.Type).ToArray(), null);
                                                currentExpression = Expression.Call(methodInfo, listParam.ToArray());
                                            }
                                        }
                                        // 静态成员(PropertyOrField)
                                        else
                                        {
                                            if (parameter != null)
                                            {
                                                currentExpression = Expression.PropertyOrField(Expression.Constant(parameter), strMember);
                                            }
                                            else
                                            {
                                                // 先找属性
                                                PropertyInfo propertyInfo = type.GetProperty(strMember, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                                if (propertyInfo != null)
                                                {
                                                    currentExpression = Expression.Property(null, propertyInfo);
                                                }
                                                // 没找到属性则找字段
                                                else
                                                {
                                                    FieldInfo fieldInfo = type.GetField(strMember, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                                    if (fieldInfo == null)
                                                    {
                                                        throw new ParseUnknownException(strMember, _codeParser.Index);
                                                    }
                                                    currentExpression = Expression.Field(null, fieldInfo);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            // 头Char不是字母或下划线
                            else
                            {
                                switch (firstChar)
                                {
                                    #region case '\"':
                                    case '\"':
                                        {
                                            string str = _codeParser.DefineString;
                                            currentExpression = Expression.Constant(str);
                                        }
                                        break;
                                    #endregion

                                    #region case '@':
                                    case '@':
                                        {
                                            string str = _codeParser.DefineString;
                                            currentExpression = Expression.Constant(str);
                                        }
                                        break;
                                    #endregion

                                    #region case '\'':
                                    case '\'':
                                        {
                                            string str = _codeParser.DefineString;
                                            currentExpression = Expression.Constant(str[0]);
                                        }
                                        break;
                                    #endregion

                                    default:
                                        {
                                            throw new ParseUnknownException(val, _codeParser.Index);
                                        }
                                }
                            }
                        }
                        break;
                    #endregion
                }
            }
            /********************** (End) 第一次读取，一元操作或一个对象 **************************/


            /********************** (Start) 第二(N)次读取，都将是二元或三元操作 **********************/
            int nextLevel = 0;
            // 若isCloseWrap为false(遇到反括号则直接返回)，且下一个操作符的优先级大于当前优先级，则计算下一个
            while ((isClosedWrap == false) && (nextLevel = TryGetNextPriorityLevel()) > priorityLevel)
            {
                string nextVal = _codeParser.ReadString();

                switch (nextVal)
                {
                    #region case "[":
                    case "[":
                        {
                            // 索引器访问
                            bool newIsClosedWrap;
                            if (currentExpression.Type.IsArray)
                            {
                                currentExpression = Expression.ArrayIndex(currentExpression, ReadExpression(0, "[", out newIsClosedWrap));
                            }
                            else
                            {
                                string indexerName = "Item";

                                object[] atts = currentExpression.Type.GetCustomAttributes(typeof(DefaultMemberAttribute), true);
                                DefaultMemberAttribute indexerNameAtt = (DefaultMemberAttribute)atts.SingleOrDefault();
                                if (indexerNameAtt != null)
                                {
                                    indexerName = indexerNameAtt.MemberName;

                                    PropertyInfo propertyInfo = currentExpression.Type.GetProperty(indexerName);
                                    MethodInfo methodInfo = propertyInfo.GetGetMethod();

                                    // 获取参数
                                    List<Expression> listParam = ReadParams(nextVal, true);

                                    currentExpression = Expression.Call(currentExpression, methodInfo, listParam);
                                }
                            }
                        }
                        break;
                    #endregion

                    #region case "]":
                    case "]":
                        {
                            if (wrapStart != "[")
                            {
                                throw new ParseUnmatchException(wrapStart, nextVal, _codeParser.Index);
                            }
                            isClosedWrap = true;
                            return currentExpression;
                        }
                    #endregion

                    #region case ")":
                    case ")":
                        {
                            if (wrapStart != "(")
                            {
                                throw new ParseUnmatchException(wrapStart, nextVal, _codeParser.Index);
                            }
                            isClosedWrap = true;
                            return currentExpression;
                        }
                    #endregion

                    #region case "}":
                    case "}":
                        {
                            if (wrapStart != "{")
                            {
                                throw new ParseUnmatchException(wrapStart, nextVal, _codeParser.Index);
                            }
                            isClosedWrap = true;
                            return currentExpression;
                        }
                    #endregion

                    #region case "+":
                    case "+":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);

                            // 其中某一个是string类型
                            if (currentExpression.Type == typeof(string) || right.Type == typeof(string))
                            {
                                // 调用string.Concat方法
                                currentExpression = Expression.Call(typeof(string).GetMethod("Concat", new Type[] { typeof(object), typeof(object) }),
                                    Expression.Convert(currentExpression, typeof(object)), Expression.Convert(right, typeof(object)));
                            }
                            else
                            {
                                AdjustNumberType(ref currentExpression, ref right);
                                currentExpression = Expression.Add(currentExpression, right);
                            }
                        }
                        break;
                    #endregion

                    #region case "-":
                    case "-":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            AdjustNumberType(ref currentExpression, ref right);
                            currentExpression = Expression.Subtract(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "*":
                    case "*":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            AdjustNumberType(ref currentExpression, ref right);
                            currentExpression = Expression.Multiply(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "/":
                    case "/":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            AdjustNumberType(ref currentExpression, ref right);
                            currentExpression = Expression.Divide(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "%":
                    case "%":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            AdjustNumberType(ref currentExpression, ref right);
                            currentExpression = Expression.Modulo(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "<<":
                    case "<<":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.LeftShift(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case ">>":
                    case ">>":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.RightShift(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case ">":
                    case ">":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.GreaterThan(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "<":
                    case "<":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.LessThan(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case ">=":
                    case ">=":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.GreaterThanOrEqual(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "<=":
                    case "<=":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.LessThanOrEqual(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "==":
                    case "==":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.Equal(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "!=":
                    case "!=":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.NotEqual(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case ".":
                    case ".":
                        {
                            string strMember = _codeParser.ReadString();
                            string strOperator = _codeParser.PeekString();
                            // 方法
                            if (strOperator == "(")
                            {
                                // 获取参数
                                List<Expression> listParam = ReadParams("(", false);

                                MethodInfo methodInfo = currentExpression.Type.GetMethod(strMember, listParam.ConvertAll<Type>(m => m.Type).ToArray());
                                currentExpression = Expression.Call(currentExpression, methodInfo, listParam.ToArray());
                            }
                            // 成员(PropertyOrField)
                            else
                            {
                                currentExpression = Expression.PropertyOrField(currentExpression, strMember);
                            }
                        }
                        break;
                    #endregion

                    #region case "is":
                    case "is":
                        {
                            Type t = ReadType(null);
                            currentExpression = Expression.TypeIs(currentExpression, t);
                        }
                        break;
                    #endregion

                    #region case "as":
                    case "as":
                        {
                            Type t = ReadType(null);
                            currentExpression = Expression.TypeAs(currentExpression, t);
                        }
                        break;
                    #endregion

                    #region case "^":
                    case "^":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.ExclusiveOr(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "&":
                    case "&":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.And(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "|":
                    case "|":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.Or(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "&&":
                    case "&&":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.AndAlso(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "||":
                    case "||":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.OrElse(currentExpression, right);
                        }
                        break;
                    #endregion

                    #region case "?":
                    case "?":
                        {
                            Expression first = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            _codeParser.ReadSymbol(":");
                            Expression second = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            currentExpression = Expression.Condition(currentExpression, first, second);
                        }
                        break;
                    #endregion

                    #region case "??":
                    case "??":
                        {
                            Expression right = ReadExpression(nextLevel, wrapStart, out isClosedWrap);
                            Expression test = Expression.Equal(currentExpression, Expression.Constant(null, currentExpression.Type));
                            currentExpression = Expression.Condition(test, right, currentExpression);
                        }
                        break;
                    #endregion

                    default:
                        throw new ParseUnknownException(nextVal, _codeParser.Index);
                }
            }
            /********************** (End) 第二(N)次读取，都将是二元或三元操作 **********************/

            return currentExpression;
        }

        /// <summary>
        /// 解析数字
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static object ParseNumber(string val)
        {
            object constVal;
            switch (val[val.Length - 1])
            {
                case 'l':
                case 'L':
                    constVal = long.Parse(val.Substring(0, val.Length - 1));
                    break;

                case 'm':
                case 'M':
                    constVal = decimal.Parse(val.Substring(0, val.Length - 1));
                    break;

                case 'f':
                case 'F':
                    constVal = float.Parse(val.Substring(0, val.Length - 1));
                    break;

                case 'd':
                case 'D':
                    constVal = double.Parse(val.Substring(0, val.Length - 1));
                    break;

                default:
                    if (val.IndexOf('.') >= 0)
                    {
                        constVal = double.Parse(val);
                    }
                    else
                    {
                        constVal = long.Parse(val);
                        if ((long)constVal <= (long)int.MaxValue && (long)constVal >= (long)int.MinValue)
                        {
                            constVal = (int)(long)constVal;
                        }
                    }
                    break;
            }
            return constVal;
        }

        /// <summary>
        /// 调整数值运算两边的类型
        /// (如一个int和一个double，则将int转换成double)
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        private void AdjustNumberType(ref Expression left, ref Expression right)
        {
            if (left.Type == right.Type)
            {
                return;
            }

            int leftLevel = _numberTypeLevel[left.Type];
            int rightLevel = _numberTypeLevel[right.Type];

            if (leftLevel > rightLevel)
            {
                right = Expression.Convert(right, left.Type);
            }
            else
            {
                left = Expression.Convert(left, right.Type);
            }
        }

        /// <summary>
        /// 读取方法调用中的参数
        /// </summary>
        /// <param name="priorityLevel">当前操作的优先级</param>
        /// <returns></returns>
        private List<Expression> ReadParams(string startSymbol, bool hasReadPre)
        {
            // 读前置括号
            if (!hasReadPre)
            {
                _codeParser.ReadSymbol(startSymbol);
            }

            // 读参数
            List<Expression> listParam = new List<Expression>();
            bool newIsClosedWrap = false;
            while (!newIsClosedWrap)
            {
                Expression expression = ReadExpression(0, startSymbol, out newIsClosedWrap);
                if (expression == null)
                {
                    break;
                }
                listParam.Add(expression);
            }
            return listParam;
        }

        /// <summary>
        /// 读取圆括号中的字符串
        /// </summary>
        /// <param name="hasReadPre">是否已经读取了前置括号</param>
        /// <returns></returns>
        private string GetBracketString(bool hasReadPre)
        {
            // 保存还原点
            CodeParserPosition position = _codeParser.SavePosition();

            // 读(
            if (!hasReadPre)
            {
                _codeParser.ReadSymbol("(");
            }

            // 读中间内容
            StringBuilder sb = new StringBuilder();
            string str = null;
            while ((str = this._codeParser.ReadString(false)) != ")")
            {
                // 读到(则表示括号有嵌套，还原，返回null
                if (str == "(")
                {
                    _codeParser.RevertPosition(position);
                    return null;
                }

                sb.Append(str);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取下一个操作的优先级。-1表示没有操作。
        /// </summary>
        /// <returns></returns>
        private int TryGetNextPriorityLevel()
        {
            string nextString = _codeParser.PeekString();
            if (string.IsNullOrEmpty(nextString) || nextString == ";" || nextString == "}" || nextString == "," || nextString == ":")
            {
                return -1;
            }

            return GetOperatorLevel(nextString, false);
        }

        /// <summary>
        /// 获取操作符的优先级，越大优先级越高
        /// </summary>
        /// <param name="operatorSymbol">操作符</param>
        /// <param name="isBefore">是否前置操作符(一元)</param>
        /// <returns>优先级</returns>
        static private int GetOperatorLevel(string operatorSymbol, bool isBefore)
        {
            switch (operatorSymbol)
            {
                case "++":
                case "--":
                    operatorSymbol += isBefore ? "before" : "behind";
                    break;

                case "+":
                case "-":
                    operatorSymbol += isBefore ? "before" : null;
                    break;
            }
            return _operatorPriorityLevel[operatorSymbol];
        }

        /// <summary>
        /// 读类型
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private Type ReadType(string val)
        {

            Type type = null;
            string strVal;
            if (string.IsNullOrEmpty(val))
            {
                strVal = _codeParser.ReadString();
            }
            else
            {
                strVal = val;
            }

            while (type == null)
            {
                // 读泛型参数
                if (_codeParser.PeekString() == "<")
                {
                    _codeParser.ReadString();
                    List<Type> listGenericType = new List<Type>();
                    while (true)
                    {
                        listGenericType.Add(ReadType(null));
                        if (_codeParser.PeekString() == ",")
                        {
                            _codeParser.ReadString();
                        }
                        else
                        {
                            break;
                        }
                    }
                    _codeParser.ReadSymbol(">");

                    strVal += string.Format("`{0}[{1}]", listGenericType.Count,
                        string.Join(",", listGenericType.Select(m => m.FullName).ToArray()));
                }

                type = GetType(strVal);
                if (type == null)
                {
                    bool result = _codeParser.ReadSymbol(".", false);
                    if (!result)
                    {
                        throw new ParseUnfindTypeException(strVal, _codeParser.Index);
                    }
                    strVal += "." + _codeParser.ReadString();
                }
            }
            return type;
        }

        /// <summary>
        /// 根据类型名称获取类型对象
        /// </summary>
        /// <param name="typeName">类型名称。可以是简写：如int、string</param>
        /// <returns></returns>
        private Type GetType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            // Nullable
            bool isNullable = false;
            if (typeName.EndsWith("?"))
            {
                isNullable = true;
                typeName = typeName.Substring(0, typeName.Length - 1);
            }
            else if(_codeParser.PeekString() == "?")
            {
                isNullable = true;
                _codeParser.ReadString();
            }

            Type type;

            switch (typeName)
            {
                case "bool":
                    type = typeof(bool);
                    break;

                case "byte":
                    type = typeof(byte);
                    break;

                case "sbyte":
                    type = typeof(sbyte);
                    break;

                case "char":
                    type = typeof(char);
                    break;

                case "decimal":
                    type = typeof(decimal);
                    break;

                case "double":
                    type = typeof(double);
                    break;

                case "float":
                    type = typeof(float);
                    break;

                case "int":
                    type = typeof(int);
                    break;

                case "uint":
                    type = typeof(uint);
                    break;

                case "long":
                    type = typeof(long);
                    break;

                case "ulong":
                    type = typeof(ulong);
                    break;

                case "object":
                    type = typeof(object);
                    break;

                case "short":
                    type = typeof(short);
                    break;

                case "ushort":
                    type = typeof(ushort);
                    break;

                case "string":
                    type = typeof(string);
                    break;

                default:
                    {
                        // 先当typeName是类的全名
                        type = GetTypeCore(typeName);

                        // 没有找到则用所有的命名空间去一次次匹配
                        if (type == null)
                        {
                            foreach (string theNamespace in this.Namespaces)
                            {
                                type = GetTypeCore(theNamespace + "." + typeName);

                                // 找到即停，不继续找（如果两个命名空间下有两个同名类，则这里永远是返回第一个，而不是报错）
                                if (type != null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    break;
            }

            if (isNullable && type != null)
            {
                type = typeof(Nullable<>).MakeGenericType(type);
            }

            return type;
        }

        /// <summary>
        /// 根据类型名称获取类型的对象
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private Type GetTypeCore(string typeName)
        {
            Assembly[] listAssembly = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in listAssembly)
            {
                if (assembly != Assembly.GetExecutingAssembly())  // 忽略当前程序集(Zhucai.TextToDelegate)
                {
                    Type type = assembly.GetType(typeName, false, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
