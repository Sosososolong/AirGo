using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DemonFox.Tails.AirGo.Models;
using System.Text;
using System.Data;
using System.Data.Common;
using DemonFox.Tails.Database.SqlServer;

namespace DemonFox.Tails.AirGo.Controllers
{
    public class HomeController : Controller
    {
        private DemonProvider db = new DemonProvider();
        public IActionResult Index()
        {
            //准备所需要的数据
            string sql = $@"select
	                            top (@pageSize) *
                            from

	                            (select
		                            ROW_NUMBER() over(order by users.Id desc) as RowNumber, *
	                            from UserInfo as users
	                            where users.IsDelete=0
                                ) as temp

                            where temp.RowNumber>(@pageIndex-1)*@pageSize
                            
                            select
	                            COUNT(*)
                            from UserInfo as users
                            where users.IsDelete=0
                            ";
            if (!string.IsNullOrEmpty(Request.Form["sql"]))
            {
                sql = Request.Form["sql"];
            }
            if (!string.IsNullOrEmpty(Request.Form["connectionString"]))
            {
                DemonProvider.ConnectionString = Request.Form["connectionString"];
            }

            if (sql.IndexOf("@pageIndex") == 0 || sql.IndexOf("@pageSize") == 0)
            {
                return Content($"sql语句: {sql}中, 没有@pageIndex和@pageSize参数");
            }
            DbParameter[] parameters = new DbParameter[2]
            {
                db.CreateDbParameter("pageIndex", 1),
                db.CreateDbParameter("pageSize", 10)
            };
            DataSet set = db.ExecuteQuerySql(sql, parameters);
            DataTable dataTable = set.Tables[0];
            List<string> columnNames = new List<string>();
            foreach (DataColumn column in dataTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }

            string newLine = Environment.NewLine;
            StringBuilder builder = new StringBuilder();
            builder.Append(@"    <title>用户管理</title>
    <link href = ""../Content/bootstrap.min.css"" rel = ""stylesheet"" />
    <script src = ""../Scripts/jquery-3.3.1.min.js"" ></ script>").Append(newLine).Append(newLine)
                .Append("----------------------------------------Body---------------------------------------").Append(newLine).Append(newLine)
                .Append(@"    <form id=""form1"" runat = ""server"" >
        < div class=""popover-content"">
            <asp:ScriptManager runat = ""server"" ID = ""MyScriptManager""></asp:ScriptManager>
            <asp:UpdatePanel runat = ""server"" ID=""TableUpdatePanel"" UpdateMode=""Conditional"">
                <ContentTemplate>
                    <table class=""table table-bordered"">
                        <tr>").Append(newLine);

            //拼接表格所有的列
            foreach (string columnName in columnNames)
            {
                builder.AppendFormat(@"                            <th class=""need - order"" field_name=""{0}"">{0}</th>", columnName).Append(newLine);
            }

            builder.Append(@"                        </tr>


                        <asp:Repeater runat=""server"" ID=""MyRepeater"">
                            <ItemTemplate>
                                <tr>").Append(newLine);

            //拼接Repeater控件中的行
            foreach (string columnName in columnNames)
            {
                builder.AppendFormat(@"                                    <td><%# Eval(""{0}"") %></td>", columnName).Append(newLine);
            }

            builder.Append(@"                                </tr>
                            </ItemTemplate>
                        </asp:Repeater>                        
                        <asp:Button runat=""server"" ID =""ReOrderBtn"" CssClass =""hidden"" OnClick =""ReOrderBtn_Click"" />
                        < asp:HiddenField runat = ""server"" ID = ""OrderFieldName"" ClientIDMode = ""Static"" />
                    </ table >

                    < div >
                        < webdiyer:AspNetPager ID = ""anpPage"" CssClass = ""pager""  CurrentPageButtonClass = ""cpb""
                            PageSize = ""10"" runat = ""server"" HorizontalAlign = ""Center"" Width = ""100% "" FirstPageText = ""首页""
                            LastPageText = ""尾页"" NextPageText = ""下一页"" PrevPageText = ""上一页"" CustomInfoHTML = ""第 %currentPageIndex%页/共%PageCount%页 每页%PageSize%条/共%RecordCount%条""
                            ShowPageIndexBox = ""Always"" ShowCustomInfoSection = ""Right""
                            CustomInfoSectionWidth = ""250px"" OnPageChanged = ""anpPage_PageChanging"" SubmitButtonClass = ""Button"" SubmitButtonText = ""转 到"" CurrentPageButtonPosition = ""Center"" >
                        </ webdiyer:AspNetPager >
                        <% --假设当前页码为2, 点击6欲跳到第6页, onpagechanging触发时CurrentPageIndex值为2, OnPageChanged触发时: CurrentPageIndex值为6-- %>
                    </ div >
                </ ContentTemplate>
            </ asp:UpdatePanel>
        </ div>
    </ form>").Append(newLine).Append(newLine);

            //拼接基本的js部分
            builder.Append(@"    <script>
        //updatepanel请求结束后触发
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            //异步刷新后也要执行 ""给列头绑定click事件的代码"", 否则异步刷新后""th""的click事件失效
            bindOrder();
        });

        function bindOrder()
        {
            $(""th"").click(function() {
                var fieldName = $(this).attr(""field_name"")
                if (fieldName.length > 0)
                {
                    OrderFieldName.value = fieldName;
                    ReOrderBtn.click();
                }
            });
        }
        bindOrder();
    </script>").Append(newLine).Append(newLine)
    .Append("----------------------------------------Code---------------------------------------").Append(newLine).Append(newLine)
    .Append(@"    {
        private DemonProvider db = new DemonProvider();
        private static string _orderByField = ""Id"";
        private static bool _isAsc = false;
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindData();
            }
        }


        private void BindData()
        {
            int pageIndex = anpPage.CurrentPageIndex;
            int pageSize = anpPage.PageSize;
            //""asc"" or ""desc""
            string orderDirection = _isAsc ? ""asc"" : ""desc"";

            //查询过滤条件
            string searchCmd = """";

            //注意查询参数, pageIndex, pageSize, _orderByField, orderDirection
            string sql = $@""select
	                            top (@pageSize) *
                            from

	                            (select
		                            ROW_NUMBER() over(order by users.{_orderByField} {orderDirection}) as RowNumber, *
	                            from UserInfo as users
	                            where users.IsDelete=0
                                {searchCmd}) as temp

                            where temp.RowNumber>(@pageIndex-1)*@pageSize
                            
                            select
	                            COUNT(*)
                            from UserInfo as users
                            where users.IsDelete=0
                            {searchCmd}"";


            DbParameter[] parameters = new DbParameter[2]
            {
                db.CreateDbParameter(""pageIndex"", pageIndex),
                db.CreateDbParameter(""pageSize"",pageSize)
            };
            DataSet set = db.ExecuteQuerySql(sql, parameters);

            //计算总页数所需要的记录总数            
            DataTable myDataTable = set.Tables[0];
            anpPage.RecordCount = Convert.ToInt32(set.Tables[1].Rows[0][0]);
            MyRepeater.DataSource = myDataTable;
            MyRepeater.DataBind();
        }

        protected void anpPage_PageChanging(object sender, EventArgs e)
        {
            BindData();
        }

        protected void ReOrderBtn_Click(object sender, EventArgs e)
        {
            anpPage.CurrentPageIndex = 1;
            if (!string.IsNullOrEmpty(OrderFieldName.Value))
            {
                _orderByField = OrderFieldName.Value == ""RowNumber"" ? ""Id"" : OrderFieldName.Value;
            }

            _isAsc = !_isAsc;
            BindData();

        }
    }").Append(newLine).Append(newLine);
            return Content(builder.ToString(), "text/plain", Encoding.UTF8);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
