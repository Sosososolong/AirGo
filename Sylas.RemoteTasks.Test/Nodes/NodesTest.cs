using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Utils;
using System.Dynamic;
using static Sylas.RemoteTasks.Utils.NodesHelper;

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
                new() { ID = 1, ParentID = 0 },
                new() { ID = 2, ParentID = 1 },
                new() { ID = 3, ParentID = 1 },

                new() { ID = 4, ParentID = 0 },
                new() { ID = 5, ParentID = 4 },
                new() { ID = 6, ParentID = 4 },
                new() { ID = 7, ParentID = 6 }
            };
            var res = GetChildren([.. nodes]);
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
            var res = GetDynamicChildren(nodes);
            Assert.NotNull(res);
            Assert.True(res.Count != 0);
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
            var res = GetDynamicChildren(nodes, "ID", "ParentID", "Children");
            Assert.NotNull(res);
            Assert.True(res.Any());
            System.Diagnostics.Debug.WriteLine(JsonConvert.SerializeObject(res));
        }
        
        [Fact]
        public void GetAllRecursively_CanGetChildren()
        {
            string items = """
                [{"id":"00925adbdc408195a6fa8b67b8cec32d","name":"学生请假申请","processName":"学生请假申请","type":"process","busId":"Process220518095028","busCode":null,"openUrl":null,"icon":null,"items":[]},
                {"id":"18095050350981692DBB43","name":"请假填报表","processName":null,"type":"form","busId":"18095050350981692DBB43","busCode":null,"openUrl":null,"icon":"fa fa-edit text-color-11 ,#fc587b","items":[]},
                {"id":"1809501120F678392EC82D","name":"请假统计表","processName":null,"type":"report","busId":"1809501120F678392EC82D","busCode":null,"openUrl":null,"icon":null,"items":[]},
                {"id":"ffe67e5b433df2631ec4975dbc7aa056","name":"链接菜单","processName":null,"type":"menu","busId":"ffe67e5b433df2631ec4975dbc7aa056","busCode":null,"openUrl":null,"icon":null,"items":[]},
                {"id":"2ac279714fa75fe509703102fcb2e09d","name":"工作台01","processName":null,"type":"portal_page","busId":"2ac279714fa75fe509703102fcb2e09d","busCode":"1e788d0d","openUrl":null,"icon":"fa fa-folder-open-o text-color-15 ,#ffd234","items":[]},
                {"id":"4f2acbf2476b798dccb9b19014e23a5b_SPZX","name":"审批中心","processName":null,"type":"menu","busId":"4f2acbf2476b798dccb9b19014e23a5b_SPZX","busCode":null,"openUrl":null,"icon":null,"items":[{"id":"0d2c676e042600e761d8f1054dd81945","name":"我的待办","processName":null,"type":"menu","busId":"0d2c676e042600e761d8f1054dd81945","busCode":null,"openUrl":"/BPM/Inbox?pn=Process220518095028&layout=0","icon":null,"items":[]},{"id":"75e856d8c915aa34ac09973c5392f73f","name":"抄送我的","processName":null,"type":"menu","busId":"75e856d8c915aa34ac09973c5392f73f","busCode":null,"openUrl":"/BPM/Notify?pn=Process220518095028&layout=0","icon":null,"items":[]},{"id":"6f8a5ed769356c6e5cab83145189db58","name":"我的申请","processName":null,"type":"menu","busId":"6f8a5ed769356c6e5cab83145189db58","busCode":null,"openUrl":"/BPM/Apply?pn=Process220518095028&layout=0","icon":null,"items":[]},{"id":"5123067886156b3d6fa750a9538757d8","name":"我的已办","processName":null,"type":"menu","busId":"5123067886156b3d6fa750a9538757d8","busCode":null,"openUrl":"/BPM/Complete?pn=Process220518095028&layout=0","icon":null,"items":[]},{"id":"0c1970b886a171c9f89d0e153322a9ff","name":"我的草稿","processName":null,"type":"menu","busId":"0c1970b886a171c9f89d0e153322a9ff","busCode":null,"openUrl":"/Extend/MyDraft?pn=Process220518095028&layout=0","icon":null,"items":[]},{"id":"6740d43e7667febe6355eee6b3af2320","name":"流程监控","processName":null,"type":"menu","busId":"6740d43e7667febe6355eee6b3af2320","busCode":null,"openUrl":"/BPM/IncidentView?pn=Process220518095028&layout=0","icon":null,"items":[]}]}]
                """;
            var itemsList = JsonConvert.DeserializeObject<List<JObject>>(items);
            Assert.NotNull(itemsList);
            var all = NodesHelper.GetAll(itemsList, "items");
            Assert.NotNull(all);
            Assert.Equal(12, all.Count);

            // 最后一项(下标为5)的第一个子项的Id
            var childId = itemsList?[5]?["items"]?[0]?["id"]?.ToString();
            Assert.NotNull(childId);
            
            // 扁平化后应该出现在索引为6的位置
            var childIdCopied = all?[6]?["id"]?.ToString();
            Assert.NotNull(childIdCopied);
            
            Assert.Equal(childId, childIdCopied);            
        }
    }
}
