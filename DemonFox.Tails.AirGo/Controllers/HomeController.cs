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

        public IActionResult GameBackend()
        {
            string sql = Request.Form["sql"];
            string connectionString = DemonProvider.ConnectionString = Request.Form["connectionString"];
            //string sql = Request.Query["sql"];
            //string connectionString = DemonProvider.ConnectionString = Request.Query["connectionString"];

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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Prepare()
        {
            return View();
        }
    }
}
