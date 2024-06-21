using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Models;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.Constants;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Controllers
{
    public class HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment, IConfiguration configuration, DatabaseProvider databaseProvider) : Controller
    {
        private readonly DotNETOperation _coreOperations = new();
        private readonly DatabaseProvider _db = databaseProvider;
        private const char _space = ' ';
        private readonly string _oneTabSpace = new(_space, 4);
        private readonly string _twoTabsSpace = new(_space, 8);
        private readonly string _fourTabsSpace = new(_space, 16);

        // ASP.NET Core 项目
        // 配置文件
        private static string _operationSettingsFile = "";
        private static string _currentOperationType = "Type001"; // 项目type001
        private static string _currentOperationPath = "Path001"; // 表示不同的项目, 相同类型的项目可能会有好几个, 解决方案目录和项目根目录相关信息

        // 所有的操作方案
        private static List<Tuple<string, JObject>> _allOperationTypes = [];

        // 当前操作方案 - settings[_currentOperationType]
        private static dynamic _settingsObj = string.Empty;

        // 当前操作方案的所有操作的细节参数配置
        private static dynamic _operationsInfo = string.Empty;

        // 数据库连接字符串
        private static string _connectionString = string.Empty;

        private static string _customDbContextFile = string.Empty;
        private readonly IWebHostEnvironment _webHostEnv = webHostEnvironment;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<HomeController> _logger = logger;

        public async Task<IActionResult> Index()
        {
            if (Request.Query.TryGetValue("ip", out _))
            {
                StringBuilder ipInfoBuilder = new();
                string? directIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
                if (!string.IsNullOrWhiteSpace(directIp))
                {
                    ipInfoBuilder.AppendLine($"Direct Ip: {directIp}");
                }
                if (Request.Headers.TryGetValue(HeaderConstants.RealIp, out Microsoft.Extensions.Primitives.StringValues value))
                {
                    ipInfoBuilder.AppendLine($"{HeaderConstants.RealIp}：{value}");
                }
                if (Request.Headers.TryGetValue(HeaderConstants.ForwardedFor, out Microsoft.Extensions.Primitives.StringValues forwardFor))
                {
                    ipInfoBuilder.AppendLine($"{HeaderConstants.ForwardedFor}：{forwardFor}");
                }
                return Content(ipInfoBuilder.ToString());
            }
            //准备所需要的数据
            string sql = $@"select
	                            top (@pageSize) *
                            from

	                            (select
		                            ROW_NUMBER() over(order by users.F_UserId desc) as RowNumber, *
	                            from {_configuration["FirstTable"]} as users
                                ) as temp

                            where temp.RowNumber>(@pageIndex-1)*@pageSize
                            
                            select
	                            COUNT(*)
                            from {_configuration["FirstTable"]} as users
                            ";
            if (Request.Method.Equals("post", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Request.Form["sql"]))
            {
                sql = Request.Form["sql"].ToString();
            }
            if (Request.Method.Equals("post", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Request.Form["connectionString"]))
            {
                _db.ConnectionString = Request.Form["connectionString"].ToString();
            }

            if (Request.Method.Equals("post", StringComparison.OrdinalIgnoreCase) && sql.StartsWith("@pageIndex") || sql.StartsWith("@pageSize"))
            {
                return Content($"sql语句: {sql}中, 没有@pageIndex和@pageSize参数");
            }
            var parameters =
            //    new DbParameter[2]
            //{
            //    db.CreateDbParameter("pageIndex", 1),
            //    db.CreateDbParameter("pageSize", 10)
            //};
            new Dictionary<string, object> {
                { "pageIndex", 1 }, { "pageSize", 10 }
            };

            DataSet set = await _db.QueryAsync(sql, parameters);
            DataTable dataTable = set.Tables[0];
            List<string> columnNames = new();
            foreach (DataColumn column in dataTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }

            string newLine = Environment.NewLine;
            StringBuilder builder = new();
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

        public async Task<IActionResult> GameBackend()
        {
            string sql = Request.Form["sql"].ToString() ?? throw new ArgumentNullException("sql");
            string connectionString = _db.ConnectionString = Request.Form["connectionString"].ToString() ?? throw new ArgumentNullException("connectionString");

            if (!sql.ToLower().Contains("@pageIndex".ToLower(), StringComparison.CurrentCulture) || !sql.ToLower().Contains("@pageSize".ToLower(), StringComparison.CurrentCulture))
            {
                return Content($"sql语句: {sql}中, 没有@pageIndex和@pageSize参数");
            }
            var parameters =
                new Dictionary<string, object> {
                    { "pageIndex", 1 }, { "pageSize", 10 }
                };

            DataSet set = await _db.QueryAsync(sql, parameters);
            DataTable dataTable = set.Tables[0];
            List<string> columnNames = new();
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

        public async Task<IActionResult> GameBackend619()
        {
            string sql = Request.Form["sql"].ToString() ?? throw new Exception("Form参数sql不能为空");
            _db.ConnectionString = Request.Form["connectionString"].ToString() ?? throw new Exception("Form参数connectionString不能为空");
            //string sql = Request.Query["sql"];
            //string connectionString = DemonProvider.ConnectionString = Request.Query["connectionString"];

            if (sql.ToUpper().IndexOf("@pageIndex".ToUpper()) == -1 || sql.ToLower().IndexOf("@pageSize".ToLower()) == -1)
            {
                return Content($"sql语句: {sql}中, 没有@pageIndex和@pageSize参数");
            }
            var parameters =
            //    new DbParameter[2]
            //{
            //    db.CreateDbParameter("pageIndex", 1),
            //    db.CreateDbParameter("pageSize", 10)
            //};
            new Dictionary<string, object> {
                { "pageIndex", 1 }, { "pageSize", 10 }
            };

            DataSet set = await _db.QueryAsync(sql, parameters);
            DataTable dataTable = set.Tables[0];
            List<string> columnNames = new();
            foreach (DataColumn column in dataTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }
            StringBuilder sb = new();
            // 假设SQL查询结果中, 主键在第二列(第一列是为排序添加的RowNumber序号)
            string primaryKey = columnNames[1];
            List<string> orderByDirectionFields = new();
            foreach (string colName in columnNames)
            {
                if (colName is null)
                {
                    continue;
                }
                if (colName.ToLower().Contains("time", StringComparison.CurrentCulture))
                {
                    orderByDirectionFields.Add($@"protected string orderBy{colName}Direction = ""desc"";");
                }
                sb.Append(_fourTabsSpace)
                    .AppendFormat(@"<th width=""50"" align=""center"" {0}>", colName.ToLower().Contains("time", StringComparison.CurrentCulture) ? $@"orderfield=""{primaryKey}"" class=""<%= orderBy{colName}Direction %>"">" : "") // 按时间排序其实就是按主键排序
                    .Append(Environment.NewLine)
                    .Append(_oneTabSpace + _fourTabsSpace)
                    .Append(colName).Append(Environment.NewLine)
                    .Append(_fourTabsSpace).Append("</th>")
                    .Append(Environment.NewLine);
            }

            // 读取模板内容
            string templateFile = "templates/game_639backend_template.html";
            FileStream fileStream = new(templateFile, FileMode.Open);
            string templateCon = "";
            using (StreamReader reader = new(fileStream))
            {
                templateCon = reader.ReadToEnd();
            }
            // 替换列标题的部分{ColumnTitles}
            Regex reg = new("{ColumnTitles}");
            string modifiedTemplate = reg.Replace(templateCon, sb.ToString());
            // 替换数据{TableRows}
            sb.Clear(); // 清空sb内容,继续使用这个对象拼接字符串            
            foreach (string colName in columnNames)
            {
                sb.Append(_twoTabsSpace + _fourTabsSpace)
                    .Append("<td>").Append(Environment.NewLine)
                    .Append(_oneTabSpace + _twoTabsSpace + _fourTabsSpace);
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
                    .Append(_twoTabsSpace + _fourTabsSpace).Append("</td>").Append(Environment.NewLine);
            }
            reg = new Regex("{TableRows}");
            modifiedTemplate = reg.Replace(modifiedTemplate, sb.ToString());
            // 替换后台代码
            sb.Clear();
            foreach (string item in orderByDirectionFields)
            {
                sb.Append(_twoTabsSpace)
                    .Append(item).Append(Environment.NewLine);
            }
            reg = new Regex("{orderByDirectionFields}");
            modifiedTemplate = reg.Replace(modifiedTemplate, sb.ToString());
            return Content(modifiedTemplate, "text/plain", Encoding.UTF8);
        }

        /// <summary>
        /// 代码生成首页视图
        /// </summary>
        /// <returns></returns>
        public IActionResult Prepare()
        {
            if (Request.Method != "GET" || _operationsInfo == null)
            {
                ViewBag.Cached = "NOCACHED";
                Init();
                SetDataForFronted();
                return View();
            }
            ViewBag.Cached = "CACHED";
            SetDataForFronted();
            return View();
        }
        /// <summary>
        /// 第一次进入程序或者每次切换项目, 需要 1.进行读取并更新配置; 2.检查数据库连接字符串(本地.db文件需要设置绝对路径); 3.扫描目标项目的完整度(各种插件是否安装完毕)
        /// </summary>
        private void Init()
        {
            // 读取配置文件并更新相关数据
            GetSettings();
            // 查看并设置数据库连接字符串
            SetDbConnectionString();
        }
        /// <summary>
        /// 获取配置文件和配置文件的配置内容(给各种字段赋值)
        /// </summary>
        private void GetSettings()
        {
            string settingsFile = Path.Combine(_webHostEnv.ContentRootPath, "solutions.json");
            if (!System.IO.File.Exists(settingsFile))
            {
                throw new Exception($"配置文件{settingsFile}不存在, 请添加配置文件!");
            }
            _operationSettingsFile = settingsFile;

            string settingsStringContent = System.IO.File.ReadAllText(settingsFile);
            // JObject 实现了IEnumerable<KeyValuePair<string, JToken?>>, 可以进行遍历
            JObject settings = FileHelper.SettingsRead(settingsFile) ?? throw new Exception("配置异常");

            // 处理配置文件中的模板变量
            settings = ReplaceSettingsTemplateVariable(settingsStringContent, settings ?? throw new Exception("配置异常"));

            // 所有的操作方案
            _allOperationTypes.RemoveAll(s => true); // 重新赋值的时候, 需要先清空
            foreach (var item in settings)
            {
                _allOperationTypes.Add(Tuple.Create(item.Key, item.Value as JObject ?? throw new Exception("配置异常")));
            }

            _settingsObj = settings[_currentOperationType] ?? throw new Exception("_currentOperationType异常");

            // 数组是JArray, 对象是JObject(实现了IDictionary, 可以按此类型遍历)   
            _operationsInfo = _settingsObj["Operations"];

            string dbContextDirectory = _settingsObj["Operations"]["InitializeCustomDbContext"]["MyDbContextDirectory"]["Path"];
            _customDbContextFile = FileHelper.FindFilesRecursive(dbContextDirectory, f => f.Contains("DbContext"), files => files.Count > 0, null).FirstOrDefault() ?? "";
        }

        private void SetDataForFronted()
        {
            ViewBag.OperationInfo = _operationsInfo;

            ViewBag.CurrentPath = _currentOperationPath;
            ViewBag.CurrentType = _currentOperationType;
            ViewBag.OperationTypes = _allOperationTypes;
            ViewBag.ProjectPathInfos = _settingsObj["Paths"];
        }

        private static JObject ReplaceSettingsTemplateVariable(string allSettingsString, JObject allSettingsObj)
        {
            JObject currentProjectPathInfo = allSettingsObj[_currentOperationType]?["Paths"]?[_currentOperationPath] as JObject ?? throw new Exception("配置异常");
            foreach (var item in currentProjectPathInfo)
            {
                if (item.Key == "ConnectionStrings")
                {
                    _connectionString = item.Value?.ToString() ?? "";
                }
                string template = $@"{{{item.Key}}}";
                string value = item.Value?.ToString() ?? "";

                allSettingsString = Regex.Replace(allSettingsString, template, value);
            }
            return JsonConvert.DeserializeObject(allSettingsString) as JObject ?? throw new Exception("配置异常");
        }
        // 读取配置文件完成后, 查看数据库连接字符串信息, 如果是本地数据库文件, 将数据库文件设置为绝对路径
        private void SetDbConnectionString()
        {
            if (_connectionString.Contains(".db"))
            {
                // sqlite数据库                
                string clientProjectDir = _settingsObj["WebAppProjectDirectory"];
                string dbFile = _connectionString.Split('=')[1];
                _db.ConnectionString = Path.Join(clientProjectDir, dbFile);
            }
        }

        // 读取配置文件, 检查数据库链接字符串后, 查看EFCore的初始化情况
        //private void CheckEFCore()
        //{
        //    string dbContextDirectory = _settingsObj["Operations"]["InitializeCustomDbContext"]["MyDbContextDirectory"]["Path"];
        //    _customDbContextFile = FileHelper.FindFilesRecursive(dbContextDirectory, f => f.Contains("DbContext"), files => files.Count > 0, null).FirstOrDefault();
        //}

        // 切换项目
        public IActionResult ChangingProject()
        {
            string target = Request.Form["target"].ToString();
            string value = Request.Form["value"].ToString();
            if (target == "type") // 对项目类型进行切换
            {
                _currentOperationPath = "Path001"; // 将项目目录切换到Path001, 因为当前的项目类型的项目路径可能只有Path001
                _currentOperationType = value ?? "";
            }
            else if (target == "path") // 同项目类型切换项目
            {
                _currentOperationPath = value ?? "";
            }
            Init();
            return Content($@"{{""code"": 1, ""msg"": """", ""data"": {JsonConvert.SerializeObject(_settingsObj["Paths"])}}}");
        }
        // 操作界面
        public IActionResult InitializeCustomDbContextPage()
        {
            // 获取所有的实体类
            string entitiesDir = _settingsObj["EntitiesDirectory"]["Path"];

            string myDbContextDir = _settingsObj["Operations"]["InitializeCustomDbContext"]["MyDbContextDirectory"]["Path"];
            if (!Directory.Exists(myDbContextDir))
            {
                Directory.CreateDirectory(myDbContextDir);
            }
            _customDbContextFile = FileHelper.FindFilesRecursive(myDbContextDir, f => f.Contains("DbContext.cs")).FirstOrDefault() ?? "";
            // 找到所有的实体/Entity
            var allEntities = FileHelper.FindFilesRecursive(entitiesDir, f => !f.Contains("Entity.cs"), null, null).Select(f => f.Replace(entitiesDir, string.Empty).Replace(".cs", string.Empty));
            // 如果已经存在自定义MyDbContext类, 操作按钮信息为modify, 并且需要过滤掉已经在MyDbContext中注册的实体集的对应的实体
            if (!string.IsNullOrEmpty(_customDbContextFile))
            {
                // MyDbContext中所有的实体集(对应一个数据表)对应的实体名称集合
                List<string> allEntitySets = _coreOperations.ToSingulars(FileHelper.GetProperties(_customDbContextFile));
                ViewBag.AllRegistedEntity = allEntitySets;
                allEntities = allEntities.Where(s => !allEntitySets.Contains(s)).Select(s => s);

                // 操作按钮信息
                ViewBag.MyDbContextBtnText = "向自定义DbContext中添加实体集属性-modify";
            }
            else
            {
                // 无需过滤实体,显示所有实体, 操作按钮信息为create
                ViewBag.MyDbContextBtnText = "自定义DbContext并添加添加实体集属性-create";
            }
            ViewBag.AllEntities = allEntities.ToList();

            return View();
        }
        // 添加自定义DbContext类 / 添加实体集属性
        public async Task<IActionResult> DbContextDoingSomething()
        {
            string dbContextFileName = "MyDbContext.cs";
            if (!string.IsNullOrWhiteSpace(Request.Form["myDbContextName"]) && Request.Form["myDbContextName"] != "MyDbContext")
            {
                dbContextFileName = Request.Form["myDbContextName"] + ".cs";
            }
            string type = Request.Form["type"].ToString(); // create modify            
            List<string> entityNamesParams = Request.Form.Where(p => p.Key.StartsWith("entity-")).Select(p => p.Key.Replace("entity-", string.Empty)).ToList();
            if (entityNamesParams.Count <= 0)
            {
                return Json(new { code = 0, msg = "请勾选对应的实体类" });
            }

            string dbContextDirectory = _settingsObj["Operations"]["InitializeCustomDbContext"]["MyDbContextDirectory"]["Path"];
            if (!Directory.Exists(dbContextDirectory))
            {
                Directory.CreateDirectory(dbContextDirectory);
            }
            string dbContextNamespace = _settingsObj["Operations"]["InitializeCustomDbContext"]["MyDbContextDirectory"]["Namespace"];
            string dbContextFilePath = Path.Join(dbContextDirectory, dbContextFileName);
            // 1. 创建或修改自定义DbContext文件, 添加实体集属性
            if (type.ToLower() == "create")
            {
                // 创建文件, 初始化文件内容
                string myDbContextContent = _coreOperations.GetMyDbContextContent(dbContextNamespace, entityNamesParams);
                await FileHelper.WriteAsync(dbContextFilePath, myDbContextContent, false);

                // 2. Startup中将自定义DbCotext注册到容器中
                string dbType = Request.Form["dbType"].ToString();
                string useMethodName = string.Empty;
                switch (dbType)
                {
                    case "0":
                        useMethodName = "UseSqlServer";
                        break;
                    case "1":
                        useMethodName = "UseMySQL";
                        break;
                    case "2":
                        useMethodName = "UseSqlite";
                        break;
                    default:
                        break;
                }
                string startupFile = _settingsObj["WebAppStartupFile"] ?? _settingsObj["WebAppProgramFile"];
                string addDbContextToContainerCodes = $@"services.AddDbContext<{dbContextFileName.Replace(".cs", string.Empty)}>(options =>
            {{
                options.{useMethodName}(""{_connectionString}""); //UseSqlServer(Microsoft.EntityFrameworkCore.SqlServer) UseMySQL(Pomelo.EntityFrameworkCore.MySql) UseSqlite(Microsoft.EntityFrameworkCore.Sqlite)
            }}); ";
                FileHelper.InsertCode(startupFile, "ConfigureServices", addDbContextToContainerCodes);
            }
            if (type.ToLower() == "modify")
            {
                // 添加实体集的代码
                StringBuilder codes = new StringBuilder();
                foreach (string entityName in entityNamesParams)
                {
                    codes.Append($"public DbSet<{entityName}> {_coreOperations.ToPlural(entityName)} {{ get; set; }}").Append(Environment.NewLine).Append(_twoTabsSpace);
                }
                FileHelper.InsertCodePropertyLevel(dbContextFilePath, codes.ToString().TrimEnd());
            }

            return Json(new { code = 1, msg = "" });
        }

        public IActionResult GetEntityProperties()
        {
            string entity = Request.Form["entityNameOfForeignKeyTable"].ToString();
            // 找到对应实体
            string entitiesDir = _settingsObj["EntitiesDirectory"]["Path"];
            string choosedEntity = FileHelper.FindFilesRecursive(entitiesDir, f => f.Contains($"/{entity}.cs") || f.Contains($"\\{entity}.cs"), null, null).FirstOrDefault() ?? "";
            List<string> entityProps = FileHelper.GetProperties(choosedEntity);
            return Json(new { code = 0, msg = entity, data = entityProps });
        }
        /// <summary>
        /// 按下"确定"按钮, 向自定义DbContext中插入两个表关系的代码
        /// </summary>
        /// <returns></returns>
        public IActionResult DbContextBuildRelationship()
        {
            // "0"一对一, "1"一对多
            string relationshipType = Request.Form["relationshipType"].ToString();
            List<string> relationshipingEntities = Request.Form.Where(p => p.Key.StartsWith("rel-entity-")).Select(p => p.Key.Replace("rel-entity-", string.Empty)).ToList();
            string foreignKeyTable = Request.Form["foreignKeyTable"].ToString();
            string foreignKey = Request.Form["foreignKey"].ToString();
            if (!relationshipingEntities.Contains(foreignKeyTable))
            {
                return Json(new { code = 0, msg = "外键表不是要建立关系的两个表之一" });
            }
            if (string.IsNullOrWhiteSpace(foreignKey))
            {
                return Json(new { code = 0, msg = "未选择外键" });
            }
            if (relationshipingEntities.Count != 2)
            {
                return Json(new { code = 0, msg = "请选择且仅选择两个实体" });
            }
            relationshipingEntities.Remove(foreignKeyTable);

            string twoTablesRelationshipCode = _coreOperations.GetTwoTablesRelationshipCode(relationshipType ?? "", foreignKeyTable, relationshipingEntities[0], foreignKey);
            if (string.IsNullOrEmpty(twoTablesRelationshipCode))
            {
                return Json(new { code = 0, msg = $"两表关系类型为: {relationshipType}, 生成的代码为空!" });
            }
            // 将指定关联关系的代码插入自定义DbContext文件中
            // FileHelper.InsertCodeToMethodTail(_customDbContextFile, "OnModelCreating", twoTablesRelationshipCode);
            FileHelper.InsertCode(
                _customDbContextFile,
                "OnModelCreating",
                twoTablesRelationshipCode,
                statement => statement.ToString().Contains(".HasOne") || statement.ToString().Contains(".WithMany"),
                statement => (statement.ToString().Contains(".HasOne") || statement.ToString().Contains(".WithMany"))
                                && (statement.ToString().Contains(foreignKeyTable) || statement.ToString().Contains(_coreOperations.ToPlural(foreignKeyTable)))
                                && statement.ToString().Contains(relationshipingEntities[0])
                );

            return Json(new { code = 1, msg = "" });
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
        /// 代码生成页面,Url地址如:http://localhost:5105/Home/CodeGen?tableFullName=Configs&tableComment=通用配置&connectionString=Server=127.0.0.1;Port=3306;Stmt=;Database=engine;Uid=root;Pwd=123456;Allow%20User%20Variables=true;&serviceFieldInController=
        /// </summary>
        /// <param name="database">从DI容器中获取的操作数据库的服务</param>
        /// <param name="tableFullName">表名</param>
        /// <param name="tableComment">表注释</param>
        /// <param name="connectionString">目标表所在的数据库的连接字符串</param>
        /// <param name="serviceFieldInController">控制器中负责业务的服务字段, 空表示创建新的服务类</param>
        /// <returns></returns>
        public async Task<IActionResult> CodeGen([FromServices] DatabaseInfo database, string tableFullName, string tableComment, string connectionString, string serviceFieldInController)
        {
            #region 参数为空跳转到配置参数的页面
            if (string.IsNullOrWhiteSpace($"{tableFullName}") || string.IsNullOrWhiteSpace($"{tableComment}") || string.IsNullOrWhiteSpace($"{connectionString}"))
            {
                return View();
            }
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                database.ChangeDatabase(connectionString);
            }
            #endregion

            #region TableFullName: 表名称; TableSimpleName: 接口名称,模型名称; TableFieldName:字段名称
            var tableSimpleName = tableFullName.EndsWith('s') ? tableFullName[..^1] : tableFullName;
            if (tableSimpleName.Contains('_'))
            {
                int startIndex = tableSimpleName.IndexOf('_') + 1;
                tableSimpleName = tableSimpleName[startIndex..];
                tableSimpleName = tableSimpleName[0].ToString().ToUpper() + tableSimpleName[1..];
            }
            tableSimpleName = $"{tableSimpleName[0].ToString().ToUpper()}{tableSimpleName[1..]}";
            var tableFieldName = $"_{tableSimpleName[0].ToString().ToLower()}{tableSimpleName[1..]}";
            ViewBag.TableFullName = tableFullName;
            ViewBag.TableComment = tableComment;
            ViewBag.TableSimpleName = tableSimpleName;
            ViewBag.TableFieldName = tableFieldName;
            #endregion

            #region Service字段; 没有传现有的Service, 那么就认为它有自己的Service
            if (string.IsNullOrWhiteSpace(serviceFieldInController))
            {
                serviceFieldInController = $"_{tableFieldName}Service";
            }
            ViewBag.ServiceFieldInController = serviceFieldInController;
            #endregion

            var columns = await database.GetTableColumnsInfoAsync(tableFullName);
            ViewBag.Columns = columns;
            return View("CodeGenPreview");
        }
    }
}