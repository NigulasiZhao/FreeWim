using System.Data;
using System.Reflection;
using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.Util;
using NPOI.XSSF.UserModel;

namespace FreeWim.Common;

public class NpoiExcelUtility
{
    /// <summary>
    /// 导出Excel
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="columnTitle"></param>
    /// <param name="title"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ExportForCommonForXlsx(DataTable dt, List<ExportDataColumn> columnTitle, string title, string path, string TimeTitle = "")
    {
        var book = new XSSFWorkbook();
        var font12 = book.CreateFont();
        font12.FontHeightInPoints = 12;
        font12.FontName = "宋体";
        font12.IsBold = false;
        font12.Color = HSSFColor.Black.Index;

        var cellStyle = book.CreateCellStyle();
        cellStyle.Alignment = HorizontalAlignment.Center;
        cellStyle.VerticalAlignment = VerticalAlignment.Center;
        cellStyle.BorderBottom = BorderStyle.Thin;
        cellStyle.BorderLeft = BorderStyle.Thin;
        cellStyle.BorderRight = BorderStyle.Thin;
        cellStyle.BorderTop = BorderStyle.Thin;
        cellStyle.WrapText = true;
        cellStyle.SetFont(font12);
        //cellStyle.FillBackgroundColor = BlueGrey.Index;

        var TitleStyle = book.CreateCellStyle();
        TitleStyle.Alignment = HorizontalAlignment.Center;
        TitleStyle.VerticalAlignment = VerticalAlignment.Center;
        TitleStyle.SetFont(font12);

        MakeSheetForCommonForXlsx(book, dt, columnTitle, title, cellStyle, TitleStyle, TimeTitle);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        var fileName = title + ".xlsx";
        using (var fsWrite = File.OpenWrite($"{path}//{fileName}"))
        {
            book.Write(fsWrite);
        }

        return fileName;
    }

    public static void MakeSheetForCommonForXlsx(XSSFWorkbook book, DataTable dt, List<ExportDataColumn> columnTitle, string title, ICellStyle cellStyle, ICellStyle TitleStyle,
        string TimeTitle = "")
    {
        var sheet = book.CreateSheet("Export") as XSSFSheet;
        var region = new CellRangeAddress(0, 0, 0, columnTitle.Count - 1);
        sheet.AddMergedRegion(region);

        #region 处理第一行

        for (var i = region.FirstRow; i <= region.LastRow; i++)
        {
            var row = sheet.CreateRow(i);
            for (var j = region.FirstColumn; j <= region.LastColumn; j++)
            {
                var singleCell = row.CreateCell((short)j);
                //singleCell.CellStyle = cellStyle;
            }
        }

        var hrow = sheet.GetRow(0);
        hrow.Height = 30 * 30;
        var icellltop0 = hrow.GetCell(0);
        icellltop0.CellStyle = TitleStyle;
        icellltop0.SetCellValue(title);

        #endregion

        #region 处理表头

        var TitleRow = sheet.CreateRow(1);
        TitleRow.Height = 20 * 20;
        for (var i = 0; i < columnTitle.Count; i++)
        {
            var cell = TitleRow.CreateCell(i);
            cell.CellStyle = cellStyle;
            cell.SetCellValue(columnTitle[i].Label);
            sheet.SetColumnWidth(i, columnTitle[i].ColumnWidth);
        }

        #endregion

        //string Gname = "";Maximum column number is 255
        //int startRows = 2;
        string[] imgArray;
        var totalLenth = 1205 * Units.EMU_PER_POINT;
        for (var i = 0; i < dt.Rows.Count; i++)
        {
            var row = sheet.CreateRow(i + 2);
            for (var j = 0; j < columnTitle.Count; j++)
            {
                var cell = row.CreateCell(j);
                cell.CellStyle = cellStyle;
                if (columnTitle[j].Prop.ToUpper() == "POS")
                {
                    cell.SetCellValue(i + 1);
                }
                else
                {
                    if (columnTitle[j].Type == "picture" && dt.Rows[i][columnTitle[j].Prop] != DBNull.Value)
                    {
                        row.Height = 5 * 300;
                        imgArray = dt.Rows[i][columnTitle[j].Prop].ToString().Split('|').Where(p => !string.IsNullOrEmpty(p)).ToArray();
                        for (var k = 0; k < imgArray.Length; k++)
                            try
                            {
                                var bytes = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imgArray[k]));
                                var pictureIdx = book.AddPicture(bytes, PictureType.JPEG);
                                var patriarch = sheet.CreateDrawingPatriarch();

                                //  HSSFClientAnchor anchor = new HSSFClientAnchor(totalLenth / imgArray.Length * k, 10 * Units.EMU_PER_POINT, -totalLenth / imgArray.Length * (imgArray.Length - (k + 1)), 0, j, i + 2, j + 1, i + 3);
                                var anchor = new HSSFClientAnchor(totalLenth / imgArray.Length * k, 0, 0, 0, j, i + 2, j + 1, i + 3);
                                //把图片插到相应的位置
                                var pict = (HSSFPicture)patriarch.CreatePicture(anchor, pictureIdx);
                            }
                            catch (Exception e)
                            {
                                continue;
                            }
                    }
                    else
                    {
                        row.CreateCell(j).SetCellValue(dt.Rows[i][columnTitle[j].Prop].ToString());
                    }
                }
            }
        }
    }

    /// <summary>
    /// 导出Excel
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="columnTitle"></param>
    /// <param name="title"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ExportForCommonNoTitle(DataTable dt, List<ExportDataColumn> columnTitle, string path)
    {
        var book = new HSSFWorkbook();
        var font12 = book.CreateFont();
        font12.FontHeightInPoints = 12;
        font12.FontName = "宋体";
        font12.IsBold = false;
        font12.Color = HSSFColor.Black.Index;

        var cellStyle = book.CreateCellStyle();
        cellStyle.Alignment = HorizontalAlignment.Center;
        cellStyle.VerticalAlignment = VerticalAlignment.Center;
        cellStyle.BorderBottom = BorderStyle.Thin;
        cellStyle.BorderLeft = BorderStyle.Thin;
        cellStyle.BorderRight = BorderStyle.Thin;
        cellStyle.BorderTop = BorderStyle.Thin;
        cellStyle.WrapText = true;
        cellStyle.DataFormat = book.CreateDataFormat().GetFormat("@");
        cellStyle.SetFont(font12);
        //cellStyle.FillBackgroundColor = BlueGrey.Index;


        var titlefont = book.CreateFont();
        titlefont.FontHeightInPoints = 11;
        titlefont.FontName = "宋体";
        titlefont.IsBold = true;
        titlefont.Color = HSSFColor.Black.Index;

        var TitleStyle = book.CreateCellStyle();
        TitleStyle.Alignment = HorizontalAlignment.Center;
        TitleStyle.VerticalAlignment = VerticalAlignment.Center;
        TitleStyle.BorderBottom = BorderStyle.Thin;
        TitleStyle.BorderLeft = BorderStyle.Thin;
        TitleStyle.BorderRight = BorderStyle.Thin;
        TitleStyle.BorderTop = BorderStyle.Thin;
        TitleStyle.FillPattern = FillPattern.SolidForeground;
        TitleStyle.WrapText = true;
        var palette = book.GetCustomPalette();
        var color = palette.FindSimilarColor(0, 204, 255);
        var colorIndex = color.Indexed;
        TitleStyle.FillForegroundColor = colorIndex;
        TitleStyle.SetFont(titlefont);

        MakeSheetForCommonNoTitle(book, dt, columnTitle, cellStyle, TitleStyle);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        var fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".xls";
        using (var fsWrite = File.OpenWrite($"{path}//{fileName}"))
        {
            book.Write(fsWrite);
        }

        return fileName;
    }

    public static void MakeSheetForCommonNoTitle(HSSFWorkbook book, DataTable dt, List<ExportDataColumn> columnTitle, ICellStyle cellStyle, ICellStyle TitleStyle)
    {
        var sheet = book.CreateSheet("Export") as HSSFSheet;

        #region 处理表头

        var TitleRow = sheet.CreateRow(0);
        TitleRow.Height = 50 * 20;
        for (var i = 0; i < columnTitle.Count; i++)
        {
            var cell = TitleRow.CreateCell(i);
            cell.CellStyle = TitleStyle;
            cell.SetCellValue(columnTitle[i].Label);
            sheet.SetColumnWidth(i, columnTitle[i].ColumnWidth);
        }

        #endregion

        for (var i = 0; i < dt.Rows.Count; i++)
        {
            var row = sheet.CreateRow(i + 1);
            for (var j = 0; j < columnTitle.Count; j++)
            {
                var cell = row.CreateCell(j);

                if (columnTitle[j].Prop.ToUpper() == "NAME")
                {
                    cell.CellStyle = cellStyle;
                    cell.SetCellValue(dt.Rows[i][columnTitle[j].Prop].ToString());
                }
                else
                {
                    cellStyle.DataFormat = book.CreateDataFormat().GetFormat("0");
                    cell.CellStyle = cellStyle;
                    if (dt.Rows[i][columnTitle[j].Prop].ToString() != "0") cell.SetCellValue(double.Parse(dt.Rows[i][columnTitle[j].Prop].ToString()));
                }
            }
        }
    }

    /// <summary>
    /// 实体列表转换成DataTable
    /// </summary>
    /// <typeparam name="T">实体</typeparam>
    /// <param name="list"> 实体列表</param>
    /// <returns></returns>
    public static DataTable ListToDataTable<T>(IList<T> list)
        where T : class
    {
        var dt = new DataTable(typeof(T).Name);
        if (list == null || list.Count <= 0) return dt;

        DataColumn column;
        DataRow row;

        var myPropertyInfo = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var length = myPropertyInfo.Length;
        var createColumn = true;

        foreach (var t in list)
        {
            if (t == null) continue;

            row = dt.NewRow();
            for (var i = 0; i < length; i++)
            {
                var pi = myPropertyInfo[i];
                var name = pi.Name;
                if (createColumn)
                {
                    column = new DataColumn(name, typeof(string));
                    dt.Columns.Add(column);
                }

                row[name] = pi.GetValue(t, null) is null ? "" : pi.GetValue(t, null).ToString();
            }

            if (createColumn) createColumn = false;

            dt.Rows.Add(row);
        }

        return dt;
    }

    public class ExportDataColumn
    {
        public string Prop { get; set; }
        public string Label { get; set; }
        public int ColumnWidth { get; set; }
        public string Type { set; get; }
    }
}