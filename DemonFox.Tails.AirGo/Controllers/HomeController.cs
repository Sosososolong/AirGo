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
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using DemonFox.Tails.Utils;

namespace DemonFox.Tails.AirGo.Controllers
{
    public class HomeController : Controller
    {
        private readonly DemonProvider db = new DemonProvider();
        private string OneTabSpace = "    ";
        private string TwoTabsSpace = "        ";
        private string FourTabsSpace = "                ";

        // ASP.NET Core 项目
        // 配置文件
        private static string _operationSettingsFile = "";
        private static string _currentOperationType = "Type001"; // 项目type001

        // 所有的操作方案
        private static List<string> _allOperationTypes = new List<string>();

        // 当前操作方案
        private static dynamic _settingsObj = null;

        // 当前操作方案的所有操作的细节参数配置
        private static dynamic _operationsInfo = null;

        // 所有操作对应的操作方法
        private static Dictionary<string, OperationDetails> Operations = new Dictionary<string, OperationDetails>(StringComparer.OrdinalIgnoreCase)
        {
            //{"000", new OperationDetails("ChangeOperationType", "修改当前所采用的操作方案", ChangeOperationType) },
            // {"001", new OperationDetails("AddApiForEntity", "为一个Entity及相关Model完成相关API", AddApiForEntity) },
            //{"002", new OperationDetails("OrderByAdd", "排序: 添加排序功能(初始化,创建一些文件)", AddOrderBy) },
            //{"003", new OperationDetails("OrderByDtoToEntityMaping", "排序: 添加Dto到Model的映射", AddDtoToEntityMapForOrderBy) },
            //{"004", new OperationDetails("AddActionConstraints", "特性: 添加Action约束(通过请求Header的媒体类型约束)", AddActionConstraints) },
            //{"005", new OperationDetails("InitializeCustomDbContext", "初始化DbContext", InitializeCustomDbContext) },
            //{"006", new OperationDetails("DbManyToManyRelationship", "多对多的表示方式", DbManyToManyRelationship) },
            //{"999", new OperationDetails("Test", "TEST: 测试程序", Test) }
        };


        /// <summary>
        /// 需要的参数："sql", "connectionString", 
        /// </summary>
        /// <returns></returns>
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
            <%--UpdateMode属性的值: Always:一个UpdatePanel中的按钮将会触发所有UpdatePanel的更新; Conditional: 每个UpdatePanel中的控件只触发控件所在UpdatePanel中内容的更新--%>
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
                <Triggers>
                    <%--异步 可指定触发当前UpdatePanel更新的控件以及控件的事件, 如果指定的是另一个UpdatePanel中的Button, 那么这个Button被点击时将会触发这两个UpdatePanel内容的更新--%>
                    <%--若干个UpdatePanel中的""AsyncPostBackTrigger""的ControlID可设置为同一个Button(或其他控件)的ID, 这样那个Button(或其他控件)可以一次触发若干个UpdatePanel内容的更新--%>
                    <%--如果页面不是很复杂, 完全可以不设置""AsyncPostBackTrigger""控件, 在UpdatePanel的UpdateModel值为""Conditional""的情况下, 每个UpdatePanel中的控件只触发当前UpdatePanel中的内容--%>
                    <%--<asp:AsyncPostBackTrigger ControlID=""Button2"" EventName=""Click"" />--%>
                </Triggers>
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

        public IActionResult GameBackend()
        {
            string sql = Request.Form["sql"];
            string connectionString = DemonProvider.ConnectionString = Request.Form["connectionString"];
            //string sql = Request.Query["sql"];
            //string connectionString = DemonProvider.ConnectionString = Request.Query["connectionString"];

            if (sql.ToLower().IndexOf("@pageIndex".ToLower()) == -1 || sql.ToLower().IndexOf("@pageSize".ToLower()) == -1)
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
            builder.Append(@"    <title></title>
    <link href=""/styles/layout.css"" rel=""stylesheet"" type=""text/css"" />
    <script src = ""/Scripts/comm.js"" type = ""text/javascript""></script>
    <script src = ""/Scripts/common.js"" type = ""text/javascript""></script>
    <script type = ""text/javascript"" src = ""/scripts/My97DatePicker/WdatePicker.js""></script>
    <style type = ""text/css"">
        .hidden{ display: none; }
        .greenColor{ color: green; }
        a{ text-decoration:none; }
        a:hover{ text-decoration:none}
        .listTitle2,.listTitle:hover{cursor:pointer;}
    </style> ").Append(newLine).Append(newLine)
                .Append("----------------------------------------Body---------------------------------------").Append(newLine).Append(newLine)
                .Append(@"    <form id=""form1"" runat=""server"">
        <!-- 头部菜单 Start -->
        <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" class=""title"">
            <tr>
                <td width=""19"" height=""25"" valign=""center"" class=""Lpd10"">
                    <div class=""arr"">
                    </div>
                </td>
                <td width=""1232"" height=""25"" valign=""center"" align=""left"">
                    <!--nmnm-->
                    你当前位置：客服中心 - XXXX
                    &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    统计：<asp:Label runat=""server"" ID=""AllCashback"" CssClass=""greenColor""></asp:Label>
                </td>
               
            </tr>
        </table>
        <!-- 头部菜单 End -->
        <table width=""100%"" border=""0"" align=""center"" cellpadding=""0"" cellspacing=""0"" class=""titleQueBg"">
            <tr>
                <td class=""listTdLeft"" style=""width: 80px"">玩家编号：</td>
                <td>
                    <asp:TextBox ID=""txtNickName"" runat=""server"" CssClass=""text""></asp:TextBox>
                    &nbsp;&nbsp;申请时间：<asp:TextBox ID=""txtStartDate"" runat=""server"" CssClass=""text"" Width=""80px"" onfocus=""WdatePicker({dateFmt:'yyyy-MM-dd',maxDate:'#F{$dp.$D(\'txtEndDate\')}'})""></asp:TextBox>
                    至 
                <asp:TextBox ID=""txtEndDate"" runat=""server"" CssClass=""text"" Width=""80px"" onfocus=""WdatePicker({dateFmt:'yyyy-MM-dd',minDate:'#F{$dp.$D(\'txtStartDate\')}'})""></asp:TextBox>
                    &nbsp;&nbsp; <%--审核状态：
                    <asp:DropDownList ID=""ddlSearchType"" runat=""server"">
                        <asp:ListItem Value=""3"">全部</asp:ListItem>
                        <asp:ListItem Value=""0"">正在审核</asp:ListItem>
                        <asp:ListItem Value=""1"">已通过</asp:ListItem>
                        <asp:ListItem Value=""2"">已驳回</asp:ListItem>                        
                    </asp:DropDownList>--%>                    
                    <asp:Button ID=""btnSearch"" runat=""server"" Text=""搜索"" CssClass=""wd2 btn"" OnClick=""btnSearch_Click"" />                   
                </td>
            </tr>
        </table>
        <div id=""content"">           
            <asp:ScriptManager ID=""ScriptManager1"" runat=""server"">
            </asp:ScriptManager>
            <%--UpdateMode属性的值: Always:一个UpdatePanel中的按钮将会触发所有UpdatePanel的更新; Conditional: 每个UpdatePanel中的控件只触发控件所在UpdatePanel中内容的更新--%>
            <asp:UpdatePanel ID = ""UpdatePanel1"" runat = ""server"" UpdateMode = ""Conditional"" >
                <ContentTemplate>
                    <table width = ""97%"" border = ""0"" align = ""center"" cellpadding = ""0"" cellspacing = ""0"" class=""box Tmg7"" id=""list"">
                        <tr align = ""center"" class=""bold"">").Append(newLine);

            //拼接表格所有的列
            foreach (string columnName in columnNames)
            {
                builder.AppendFormat(@"                            <th class=""listTitle2 need-order"" field_name=""{0}"">{0}</th>", columnName).Append(newLine);
            }

            builder.Append(@"                        </tr>
                        <asp:Repeater ID=""rptUserList"" runat=""server"">
                            <ItemTemplate >
                                <tr align = ""center"" class=""list"" onmouseover=""currentcolor=this.style.backgroundColor;this.style.backgroundColor='#caebfc';this.style.cursor='default';""
                                    onmouseout=""this.style.backgroundColor=currentcolor"">").Append(newLine);
            //奇数行
            foreach (string columnName in columnNames)
            {
                builder.Append($@"                                    <td>
                                        <%# Eval(""{columnName}"") %>
                                    </td> ").Append(newLine);
            }
            //偶数行
            builder.Append(@"                                </tr>
                            </ItemTemplate>
                            <AlternatingItemTemplate>
                                <tr align=""center"" class=""listBg"" onmouseover=""currentcolor = this.style.backgroundColor; this.style.backgroundColor = '#caebfc'; this.style.cursor = 'default'; ""
                                    onmouseout = ""this.style.backgroundColor=currentcolor"">").Append(newLine);

            foreach (string columnName in columnNames)
            {
                builder.Append($@"                                    <td>
                                        <%# Eval(""{columnName}"") %>
                                    </td>").Append(newLine);
            }

            builder.Append(@"                                </tr>
                            </AlternatingItemTemplate>
                        </asp:Repeater>
                        <asp:Literal runat=""server"" ID=""litNoData"" Visible=""true"" Text=""<tr class='tdbg'><td colspan = '100' align='center'><br>没有任何信息!<br><br></td></tr>""></asp:Literal>
                        <asp:Button runat = ""server"" ID =""ReOrderBtn"" CssClass =""hidden"" OnClick =""ReOrderBtn_Click"" />
                        <asp:Button runat=""server"" ID =""RefreshBtn"" CssClass =""hidden"" OnClick =""RefreshBtn_Click"" />
                        <asp:HiddenField runat = ""server"" ID = ""OrderFieldName"" ClientIDMode = ""Static"" />
                    </table>

                    <table width = ""100%"" border=""0"" align=""center"" cellpadding=""0"" cellspacing=""0"">
                        <tr>
                            <td align = ""right"" class=""page"">
                                <webdiyer:AspNetPager UrlPaging = ""false"" ID=""anpUserList"" runat=""server"" OnPageChanged=""anpInsure_PageChanged"" AlwaysShow=""false"" FirstPageText=""首页"" LastPageText=""末页""
                                    PageSize=""14"" NextPageText=""下页"" PrevPageText=""上页"" ShowBoxThreshold=""0"" ShowCustomInfoSection=""Left"" LayoutType=""Table""
                                    NumericButtonCount=""5"" CustomInfoHTML=""总记录：%RecordCount%　页码：%CurrentPageIndex%/%PageCount%　每页：%PageSize%"">
                                </webdiyer:AspNetPager>
                            </td>
                        </tr>
                    </table>
                </ContentTemplate>
                <Triggers>
                    <%--异步 可指定触发当前UpdatePanel更新的控件以及控件的事件, 如果指定的是另一个UpdatePanel中的Button, 那么这个Button被点击时将会触发这两个UpdatePanel内容的更新--%>
                    <%--若干个UpdatePanel中的""AsyncPostBackTrigger""的ControlID可设置为同一个Button(或其他控件)的ID, 这样那个Button(或其他控件)可以一次触发若干个UpdatePanel内容的更新--%>
                    <%--如果页面不是很复杂, 完全可以不设置""AsyncPostBackTrigger""控件, 在UpdatePanel的UpdateModel值为""Conditional""的情况下, 每个UpdatePanel中的控件只触发当前UpdatePanel中的内容--%>
                    <%--<asp:AsyncPostBackTrigger ControlID=""Button2"" EventName=""Click"" />--%>
                </Triggers>
            </asp:UpdatePanel>
        </div>        
    </form>
    <script>
        //updatepanel请求结束后触发
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            //异步刷新后也要执行 ""给列头绑定click事件的代码"", 否则异步刷新后""th""的click事件失效
            bindOrder();
        });
        function bindOrder() {
            $(""th"").click(function() {
                var fieldName = $(this).attr(""field_name"")
                if (fieldName.length > 0) {
                    OrderFieldName.value = fieldName;
                    ReOrderBtn.click();
                }
            });
        }
        bindOrder();                
        //重新加载表格-当前分页
        function reloadTable() {
            document.getElementById(""RefreshBtn"").click();
        }
    </script>").Append(newLine).Append(newLine)
    .Append("----------------------------------------Code---------------------------------------").Append(newLine).Append(newLine)
    .Append(@"        private static string _orderByField = ""record.RecordID"";
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
            string searchStr = """";
            //1. 用户编号
            string gameId = txtNickName.Text;
            if (!string.IsNullOrEmpty(gameId))
            {
                searchStr += "" and account.GameID like '%"" + gameId + ""%' "";
            }
            //2. 时间
            string startTimeStr = string.IsNullOrEmpty(txtStartDate.Text) ? ""1970-01-01"" : txtStartDate.Text;
            string endTimeStr = string.IsNullOrEmpty(txtEndDate.Text) ? DateTime.Now.AddDays(1).ToString(""yyyy-MM-dd"") : Convert.ToDateTime(txtEndDate.Text).AddDays(1).ToString(""yyyy-MM-dd"");
            //当开始时间晚于结束时间，对调
            if (DateTime.Compare(Convert.ToDateTime(startTimeStr), Convert.ToDateTime(endTimeStr)) > 0)
            {
                string temp = startTimeStr;
                startTimeStr = endTimeStr;
                endTimeStr = temp;
            }
            searchStr += $"" and record.CollectDate between '{startTimeStr}' and '{endTimeStr}'"";
            //3. 是否通过, 0：正在审核   1：通过    2：驳回    3.显示全部信息
            //int status = Convert.ToInt32(ddlSearchType.SelectedValue);
            //if (status != 3)
            //{
            //    searchStr += $"" and records.RebateStatus = {status}"";
            //}

            //获取数据
            int pageIndex = anpUserList.CurrentPageIndex;

            int pageSize = anpUserList.PageSize;

            DataSet dataSet;
            string orderByDirection = _isAsc ? ""ASC"" : ""DESC"";
            if (string.IsNullOrEmpty(_orderByField))
            {
                //aideGUser=new GUserFacade();
                dataSet = aideGUser.GetTransferList(pageIndex, pageSize, _orderByField, orderByDirection, searchStr);
            }
            else
            {
                dataSet = aideGUser.GetTransferList(pageIndex, pageSize, _orderByField, orderByDirection, searchStr);
            }


            if (dataSet.Tables[0].Rows.Count > 0)
            {
                litNoData.Visible = false;
            }
            else
            {
                litNoData.Visible = true;
            }

            rptUserList.DataSource = dataSet.Tables[0];
            rptUserList.DataBind();
            anpUserList.RecordCount = Convert.ToInt32(dataSet.Tables[1].Rows[0][0]);

            //所有玩家的 返现请求总和  nmnm
            AllCashback.Text = !string.IsNullOrEmpty(dataSet.Tables[1].Rows[0][""AllSwap""].ToString()) ?
                TextUtility.FormatMoney(Convert.ToDecimal(dataSet.Tables[1].Rows[0][""AllSwap""].ToString()) / pic) :
                ""0.00"";
        }        

        protected void anpInsure_PageChanged(object sender, EventArgs e)
        {
            BindData();
        }
        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name=""sender""></param>
        /// <param name=""e""></param>
        protected void btnSearch_Click(object sender, EventArgs e)
        {
            //搜索显示第一页信息（数据少的时候只有一页信息）
            anpUserList.CurrentPageIndex = 1;
            BindData();
        }
        /// <summary>
        /// 排序
        /// </summary>
        /// <param name=""sender""></param>
        /// <param name=""e""></param>
        protected void ReOrderBtn_Click(object sender, EventArgs e)
        {
            anpUserList.CurrentPageIndex = 1;
            if (!string.IsNullOrEmpty(OrderFieldName.Value))
            {
                _orderByField = OrderFieldName.Value == ""RowNumber"" ? ""Id"" : OrderFieldName.Value;//nmnm
            }

            _isAsc = !_isAsc;
            BindData();
        }
        /// <summary>
        /// 刷新当前页（因为分页控件的 CurrentPageIndex 属性记录了当前页，所以pageIndex 值还是当前页，重新查询数据即可）
        /// </summary>
        /// <param name=""sender""></param>
        /// <param name=""e""></param>
        protected void RefreshBtn_Click(object sender, EventArgs e)
        {
            BindData();
        }").Append(newLine).Append(newLine);
            return Content(builder.ToString(), "text/plain", Encoding.UTF8);
        }

        public IActionResult GameBackend619()
        {
            string sql = Request.Form["sql"];
            DemonProvider.ConnectionString = Request.Form["connectionString"];
            //string sql = Request.Query["sql"];
            //string connectionString = DemonProvider.ConnectionString = Request.Query["connectionString"];

            if (sql.ToUpper().IndexOf("@pageIndex".ToUpper()) == -1 || sql.ToLower().IndexOf("@pageSize".ToLower()) == -1)
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
            StringBuilder sb = new StringBuilder();
            // 假设SQL查询结果中, 主键在第二列(第一列是为排序添加的RowNumber序号)
            string primaryKey = columnNames[1];
            List<string> orderByDirectionFields = new List<string>();
            foreach (string colName in columnNames)
            {
                if (colName.ToLower().IndexOf("time") >= 0)
                {
                    orderByDirectionFields.Add($@"protected string orderBy{colName}Direction = ""desc"";");
                }
                sb.Append(FourTabsSpace)
                    .AppendFormat(@"<th width=""50"" align=""center"" {0}>", colName.ToLower().IndexOf("time") >= 0 ? $@"orderfield=""{primaryKey}"" class=""<%= orderBy{colName}Direction %>"">" : "") // 按时间排序其实就是按主键排序
                    .Append(Environment.NewLine)
                    .Append(OneTabSpace + FourTabsSpace)
                    .Append(colName).Append(Environment.NewLine)
                    .Append(FourTabsSpace).Append("</th>")
                    .Append(Environment.NewLine);
            }
            
            // 读取模板内容
            string templateFile = "templates/game_639backend_template.html";
            FileStream fileStream = new FileStream(templateFile, FileMode.Open);
            string templateCon = "";
            using (StreamReader reader = new StreamReader(fileStream))
            {                
                templateCon = reader.ReadToEnd();       
            }
            // 替换列标题的部分{ColumnTitles}
            Regex reg = new Regex("{ColumnTitles}");
            string modifiedTemplate = reg.Replace(templateCon, sb.ToString());
            // 替换数据{TableRows}
            sb.Clear(); // 清空sb内容,继续使用这个对象拼接字符串            
            foreach (string colName in columnNames)
            {
                sb.Append(TwoTabsSpace + FourTabsSpace)
                    .Append("<td>").Append(Environment.NewLine)
                    .Append(OneTabSpace + TwoTabsSpace + FourTabsSpace);
                if (columnNames.Contains("UserID") && (colName.IndexOf("GameID") != -1 || colName.IndexOf("Accounts") != -1 || colName.IndexOf("NickName") != -1))
                {
                    sb.Append($@"<a class=""edit"" href ='/Module/AccountManager/UserInfo.aspx?relId=<%=relId %>&userid=<%#((DataRowView)Container.DataItem)[""UserID""]%>'
                                target = ""navTab"" rel = ""edituser<%=relId %>"" title = ""玩家明细"" >
                                <%#Eval(""{colName}"")%></a>");
                }
                else
                {
                    sb.Append($@"<%# Eval(""{colName}"") %>");
                }
                sb.Append(Environment.NewLine)
                    .Append(TwoTabsSpace + FourTabsSpace).Append("</td>").Append(Environment.NewLine);
            }
            reg = new Regex("{TableRows}");
            modifiedTemplate = reg.Replace(modifiedTemplate, sb.ToString());
            // 替换后台代码
            sb.Clear();
            foreach (string item in orderByDirectionFields)
            {
                sb.Append(TwoTabsSpace)
                    .Append(item).Append(Environment.NewLine);                    
            }
            reg = new Regex("{orderByDirectionFields}");
            modifiedTemplate = reg.Replace(modifiedTemplate, sb.ToString());            
            return Content(modifiedTemplate, "text/plain", Encoding.UTF8);
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
        /// <summary>
        /// 代码生成首页视图
        /// </summary>
        /// <returns></returns>
        public IActionResult Prepare()
        {
            Init();
            return View();
        }

        private void Init()
        {
            // 读取配置
            GetSettings();
            // 界面渲染结束
            // Start();
        }
        /// <summary>
        /// 获取配置文件和配置文件的配置内容
        /// </summary>
        private void GetSettings()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string settingsFile = baseDir.Remove(baseDir.IndexOf(@"\bin")) + @"\solutions.json";
            if (!System.IO.File.Exists(settingsFile))
            {
                Console.WriteLine($"配置文件{settingsFile}不存在, 将在根目录寻找配置文件");
                settingsFile = baseDir + @"\solutions.json";
                if (!System.IO.File.Exists(settingsFile))
                {
                    throw new Exception($"配置文件{settingsFile}不存在, 请添加配置文件再重新启动程序!");                    
                }
                _operationSettingsFile = settingsFile;
            }

            // JObject 实现了IEnumerable<KeyValuePair<string, JToken?>>, 可以进行遍历
            JObject settings = FileOp.SettingsRead(settingsFile);

            // 所有的操作方案
            foreach (var item in settings)
            {
                _allOperationTypes.Add(item.Key);
            }

            _settingsObj = settings[_currentOperationType];

            _operationsInfo = _settingsObj["Operations"];
        }
        /// <summary>
        /// 显示操作面板
        /// </summary>
        //private void Start()
        //{
        //    while (true)
        //    {
        //        Console.WriteLine();
        //        Console.WriteLine("请选择需要的操作:");
        //        ShowOperations();
        //        string operationNo = Console.ReadLine();

        //        OperationDetails op = null;
        //        if (Operations.ContainsKey(operationNo))
        //        {
        //            op = Operations[operationNo];
        //        }
        //        if (op == null)
        //        {
        //            Console.WriteLine($"操作编号{operationNo}输入不正确, 请重新输入");
        //            continue;
        //        }

        //        // 已经正确得到用户选择的操作, 执行操作
        //        try
        //        {
        //            op.OperationHandler(op);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("出错了: " + ex.Message);
        //            continue;
        //        }

        //        Console.WriteLine("执行已经完成...");
        //    }
        //}
        private void ShowOperations()
        {
            // 从配置文件获取当前解决方案的所有操作(名称)
            JObject ops = _settingsObj["Operations"]; // 数组是JArray, 得到对象是JObject(实现了IDictionary, 可以按此类型遍历)
            List<string> allOperationNamesOfCurrentSolution = new List<string>();
            foreach (var item in ops)
            {
                allOperationNamesOfCurrentSolution.Add(item.Key.ToString());
            }
            // 为了保证随时能够切换方案和执行测试代码
            allOperationNamesOfCurrentSolution.Add("ChangeOperationType");
            allOperationNamesOfCurrentSolution.Add("Test");
            allOperationNamesOfCurrentSolution.Add("DbManyToManyRelationship");
            // 过滤出当前操作方案里面有的操作
            var neededOperations = Operations.Where(kv => allOperationNamesOfCurrentSolution.Contains(kv.Value.OperationName)).Select(kv => kv).ToList();
            foreach (var item in neededOperations)
            {
                Console.WriteLine(item.Key + " " + item.Value.OperationDescribe);
            }
        }


    }
}
