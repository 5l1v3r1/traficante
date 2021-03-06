﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Traficante.TSQL.Evaluator.Tests.Core.Schema;
using Traficante.TSQL.Tests;

namespace Traficante.TSQL.Evaluator.Tests.Core
{
    [TestClass]
    public class FunctionsTests : TestBase
    {
        [TestMethod]
        public void Select_ISNULL()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.SetVariable("@alwayson", (int?)null);

            var result = sut.RunAndReturnTable("SELECT ISNULL(@alwayson, -1) AS [AlwaysOn]");
            Assert.IsNotNull(result);
            Assert.AreEqual("AlwaysOn", result.Columns.First().ColumnName);
            Assert.AreEqual(-1, result[0][0]);
        }

        [TestMethod]
        public void Select_Function_WithDatabaseAndSchema()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.AddFunction<bool?>("fn_syspolicy_is_automation_enabled", new string[2] { "msdb", "dbo" }, () => true);

            var result = sut.RunAndReturnTable("SELECT msdb.dbo.fn_syspolicy_is_automation_enabled()");
            Assert.IsNotNull(result);
            Assert.AreEqual("msdb.dbo.fn_syspolicy_is_automation_enabled()", result.Columns.First().ColumnName);
            Assert.AreEqual(true, result[0][0]);
        }


        [TestMethod]
        public void Execute_FunctionWithArguments_AssigneResultToVariable()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.SetVariable("@alwayson", (int?)null);
            sut.SetVariable("@@SERVICENAME", "Traficante");
            sut.AddFunction<string, string, int>("xp_qv", new string[2] { "master", "dbo" }, (x, y) => 5);

            var result = sut.RunAndReturnTable("EXECUTE @alwayson = master.dbo.xp_qv N'3641190370', @@SERVICENAME;");
            var alwayson = sut.GetVariable("@alwayson");
            Assert.IsNotNull(alwayson);
            Assert.AreEqual(5, alwayson.Value);
        }

        static bool Execute_FunctionWithArguments_Flag = false;
        [TestMethod]
        public void Execute_FunctionWithArguments()
        {
            TSQLEngine sut = new TSQLEngine();

            sut.SetVariable("@@SERVICENAME", "Traficante");
            sut.AddFunction<string, string, int>("xp_qv", new string[2] { "master", "dbo" }, (x, y) =>
            {
                Execute_FunctionWithArguments_Flag = true;
                return default(int);
            });

            sut.RunAndReturnTable("EXECUTE master.dbo.xp_qv N'3641190370', @@SERVICENAME;");
            Assert.IsTrue(Execute_FunctionWithArguments_Flag);
        }

        [TestMethod]
        public void Select_From_FunctionWithoutArguments()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.AddFunction("get_entities", () =>
            {
                return new[]
                    {
                        new BasicEntity("may"),
                        new BasicEntity("june")
                    }.AsEnumerable();
            });

            var table = sut.RunAndReturnTable("select * from get_entities()");

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("may", table[0][0]);
            Assert.AreEqual("june", table[1][0]);
        }

        [TestMethod]
        public void SelectOneColumn_From_FunctionWithoutArguments()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.AddFunction("get_entities", () =>
            {
                return new[]
                    {
                        new BasicEntity("may"),
                        new BasicEntity("june")
                    }.AsEnumerable();
            });

            var table = sut.RunAndReturnTable("select Name from get_entities()");

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("may", table[0][0]);
            Assert.AreEqual("june", table[1][0]);
        }

        [TestMethod]
        public void Select_From_FunctionArguments()
        {
            TSQLEngine sut = new TSQLEngine();
            sut.AddFunction("get_entities", (int? a, string b) =>
            {
                return new[]
                    {
                        new BasicEntity(a.ToString()),
                        new BasicEntity(b)
                    }.AsEnumerable();
            });

            var table = sut.RunAndReturnTable("select * from get_entities(3, 'june')");

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("3", table[0][0]);
            Assert.AreEqual("june", table[1][0]);
        }

        [TestMethod]
        public void Select_FromDataReader_FunctionArguments()
        {
            using (TSQLEngine sut = new TSQLEngine())
            {
                sut.AddFunction("get_entities", (int? a, string b) =>
                {

                    var reader = new StreamReader("csv.csv");
                    var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture, false);
                    return new CsvDataReader(csvReader);
                });

                var table = sut.RunAndReturnTable("select * from get_entities(3, 'june')");

                Assert.AreEqual(2, table.Count);
                Assert.AreEqual("1", table[0][0]);
                Assert.AreEqual("one", table[0][1]);
                Assert.AreEqual("2", table[1][0]);
                Assert.AreEqual("two", table[1][1]);
            }
        }

        [TestMethod]
        public void Select_Function_WithoutFrom()
        {
            var query = "select GetDate()";

            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("may", 100m) { Population = -100 },
                        new BasicEntity("june", 200m) { Population = 200 }
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual(1, table.Count);

            Assert.IsTrue(table[0][0] is DateTimeOffset);
        }

        [TestMethod]
        public void CoalesceTest()
        {
            var query = @"select Coalesce('a', 'b', 'c', 'e', 'f') from #A.entities()";

            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("A")
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual("a", table[0][0]);
        }

        [TestMethod]
        public void ChooseTest()
        {
            var query = @"select Choose(2, 'a', 'b', 'c', 'e', 'f') from #A.entities()";

            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("A")
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual("c", table[0][0]);
        }

        [TestMethod]
        public void SelectWithAbsFunction()
        {
            var query = "select abs(Population) from #A.entities";

            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("may", 100m) { Population = -100 },
                        new BasicEntity("june", 200m) { Population = 200 }
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual(2, table.Count);

            Assert.AreEqual(100m, table[0][0]);
            Assert.AreEqual(200m, table[1][0]); ;
        }

        [TestMethod]
        public void Select_Function_WithFrom()
        {
            var query = "select GetDate() from #A.entities";

            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("may", 100m) { Population = -100 },
                        new BasicEntity("june", 200m) { Population = 200 }
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual(2, table.Count);

            Assert.IsTrue(table[0][0] is DateTimeOffset);
            Assert.IsTrue(table[1][0] is DateTimeOffset);
        }

        [TestMethod]
        public void SimpleRowNumberStatTest()
        {
            var query = @"select RowNumber() from #A.Entities()";
            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("001"),
                        new BasicEntity("002"),
                        new BasicEntity("003"),
                        new BasicEntity("004"),
                        new BasicEntity("005"),
                        new BasicEntity("006")
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual(6, table.Count);
            Assert.AreEqual((int?)1, table[0].Values[0]);
            Assert.AreEqual((int?)2, table[1].Values[0]);
            Assert.AreEqual((int?)3, table[2].Values[0]);
            Assert.AreEqual((int?)4, table[3].Values[0]);
            Assert.AreEqual((int?)5, table[4].Values[0]);
            Assert.AreEqual((int?)6, table[5].Values[0]);
        }

        [TestMethod]
        public void CallMethodWithTwoParametersTest()
        {
            var query = @"select Concat(Country, ToString(Population)) from #A.Entities()";
            var sources = new Dictionary<string, IEnumerable<BasicEntity>>
            {
                {
                    "#A", new[]
                    {
                        new BasicEntity("ABBA", 200)
                    }
                }
            };

            var vm = CreateAndRunVirtualMachine(query, sources);
            var table = vm.Run();

            Assert.AreEqual(1, table.Columns.Count());
            Assert.AreEqual("Concat(Country, ToString(Population))", table.Columns.ElementAt(0).ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(0).ColumnType);

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("ABBA200", table[0].Values[0]);
        }





    }
}