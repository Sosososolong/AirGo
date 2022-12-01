using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Utils;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sylas.RemoteTasks.App.Utils.NodesHelper;

namespace Sylas.RemoteTasks.Test.Nodes
{
    public class NodesTest
    {
        /// <summary>
        /// 测试Node类型的节点
        /// </summary>
        [Fact]
        public void GetChildrenNodes()
        {
            var nodes = new Node[] {
                new Node { ID = 1, ParentID = 0 },
                new Node { ID = 2, ParentID = 1 },
                new Node { ID = 3, ParentID = 1 },

                new Node { ID = 4, ParentID = 0 },
                new Node { ID = 5, ParentID = 4 },
                new Node { ID = 6, ParentID = 4 },
                new Node { ID = 7, ParentID = 6 }
            };
            var res = GetChildrenRecursively(nodes.ToList());
            Assert.NotNull(res);
            Assert.True(res.Any());
            System.Diagnostics.Debug.WriteLine(JsonConvert.SerializeObject(res));
        }

        /// <summary>
        /// 测试dynamic类型的节点
        /// </summary>
        [Fact]
        public void GetDynamicChildrenNodes()
        {
            var nodes = new List<dynamic>();
            dynamic n1 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n1);
            dynamic n2 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n2);
            dynamic n3 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n3);
            dynamic n4 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n4);
            dynamic n5 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n5);
            dynamic n6 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n6);
            dynamic n7 = new ExpandoObject(); n1.ID = 1; n1.ParentID = 0; nodes.Add(n7);
            var res = GetDynamicChildrenRecursively(nodes);
            Assert.NotNull(res);
            Assert.True(res.Any());
            System.Diagnostics.Debug.WriteLine(JsonConvert.SerializeObject(res));
        }
        
        /// <summary>
        /// 测试JArray/JObject类型的节点
        /// </summary>
        [Fact]
        public void GetJArrayChildrenNodes()
        {
            var nodes = new JArray {
                new JObject { { "ID", 1 }, { "ParentID", 0 } },
                new JObject { { "ID", 2 }, { "ParentID", 1 } },
                new JObject { { "ID", 3 }, { "ParentID", 1 } },

                new JObject { { "ID", 4 }, { "ParentID", 0 } },
                new JObject { { "ID", 5 }, { "ParentID", 4 } },
                new JObject { { "ID", 6 }, { "ParentID", 4 } },
                new JObject { { "ID", 7 }, { "ParentID", 6 } }
            };
            var res = GetDynamicChildrenRecursively(nodes, "ID", "ParentID", "Children");
            Assert.NotNull(res);
            Assert.True(res.Any());
            System.Diagnostics.Debug.WriteLine(JsonConvert.SerializeObject(res));
        }
    }
}
