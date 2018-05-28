﻿using InfluxData.Net.InfluxDb.Models.Responses;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace Hspi
{
    using static System.FormattableString;

    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private static string FirstCharToUpper(string input)
        {
            switch (input)
            {
                case null: return null;
                case "": return string.Empty;
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        private static string ProcessInfluxDBDateTime(CultureInfo culture, string dateTimePattern, long timePoint)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timePoint).ToLocalTime();
            return dateTime.ToString(dateTimePattern, culture);
        }

        private string BuildHistoryPage(NameValueCollection parts, DevicePersistenceData data)
        {
            StringBuilder stb = new StringBuilder();
            IncludeDataTableFiles(stb);

            HSHelper hsHelper = new HSHelper(HS);

            string header = Invariant($"History - {hsHelper.GetName(data.DeviceRefId)}");

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceHistory", "IdHistory", "Post"));
            stb.Append(@"<div>");

            stb.Append(@"<table class='full_width_table'>");

            stb.Append(Invariant($"<tr><td class='tableheader'>{header}</td></tr>"));
            var queries = GetDefaultValueQueries(data);
            string querySelection = parts[QueryPartId];
            if (string.IsNullOrWhiteSpace(querySelection))
            {
                querySelection = queries?.FirstOrDefault().Key;
            }

            NameValueCollection collection = new NameValueCollection();
            foreach (var query in queries)
            {
                collection.Add(query.Key, query.Key);
            }

            stb.Append(Invariant($"<tr><td>{FormDropDown(HistoryQueryTypeId, collection, querySelection, 400, string.Empty, true)}</td></tr>"));

            string finalQuery = Invariant(queries[querySelection]);
            stb.Append(Invariant($"<tr height='10'><td>{HtmlTextBox(RecordId, data.Id.ToString(), @type: "hidden")}</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(QueryTestDivId, string.Empty));
            stb.Append(Invariant($"{TextArea(QueryTestId, finalQuery)}"));
            stb.Append(DivEnd());
            stb.Append(Invariant($"<br>{FormButton(HistoryRunQueryButtonName, "Run Query", "Run Query")}</td></tr>"));
            stb.Append("<tr height='5'><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader'>Results</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(HistoryResultDivId, string.Empty));
            BuildTable(finalQuery, stb, 25);
            stb.Append(DivEnd());
            stb.Append("</td><tr>");
            stb.Append("<tr height='5'><td></td></tr>");
            stb.Append(Invariant($"<tr><td>{HistoryBackButton()}</td></tr>"));
            stb.Append("</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        private void IncludeDataTableFiles(StringBuilder stb)
        {
            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");
        }

        private void BuildTable(string query, StringBuilder stb, int pageLength)
        {
            try
            {
                var culture = CultureInfo.InvariantCulture;
                var queryData = GetData(query).ToArray();

                if (queryData.Length > 0)
                {
                    int columns = queryData[0].Columns.Count;

                    stb.Append("<table id=\"results\" class=\"cell-border compact\" style=\"width:100%\">");
                    stb.Append(@"<thead><tr>");
                    foreach (var column in queryData[0].Columns)
                    {
                        stb.Append(Invariant($"<th>{ HttpUtility.HtmlEncode(FirstCharToUpper(column))}</th>"));
                    }
                    stb.Append(@"</tr></thead>");
                    stb.Append(@"<tbody>");

                    string dateTimePattern = CultureInfo.CurrentUICulture.DateTimeFormat.LongDatePattern +
                                     " " + CultureInfo.CurrentUICulture.DateTimeFormat.LongTimePattern;

                    foreach (var row in queryData[0].Values)
                    {
                        stb.Append(@"<tr>");
                        for (int i = 0; i < row.Count; i++)
                        {
                            object column = row[i];
                            string value = string.Empty;
                            string sortValue = null;

                            if (i == 0)
                            {
                                var timePoint = Convert.ToInt64(column, CultureInfo.InvariantCulture);
                                sortValue = column.ToString();
                                value = ProcessInfluxDBDateTime(culture, dateTimePattern, timePoint);
                            }
                            else
                            {
                                switch (column)
                                {
                                    case double doubleValue:
                                        value = doubleValue.ToString("N3", culture);
                                        break;

                                    case float floatValue:
                                        value = floatValue.ToString("N3", culture);
                                        break;

                                    case null:
                                        break;

                                    default:
                                        value = Convert.ToString(column, culture);
                                        break;
                                }
                            }

                            if (sortValue != null)
                            {
                                stb.Append(Invariant($"<td data-order='{HttpUtility.HtmlEncode(sortValue)}' class='tablecell'>{ HttpUtility.HtmlEncode(value)}</td>"));
                            }
                            else
                            {
                                stb.Append(Invariant($"<td class='tablecell'>{HttpUtility.HtmlEncode(value)}</td>"));
                            }
                        }
                        stb.Append(@"</tr>");
                    }
                    stb.Append(@"</tbody>");
                    stb.Append(@"</table>");

                    stb.AppendLine("<script type='text/javascript'>");
                    stb.AppendLine(@"$(document).ready(function() {");
                    stb.AppendLine(@"$('#results').DataTable({
                                       'pageLength':25,
                                        'order': [],");
                    stb.AppendLine(Invariant($"  'pageLength': {pageLength}, "));
                    stb.AppendLine(@"   'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ]
                                    });
                                });");
                    stb.AppendLine("</script>");
                }
            }
            catch (Exception ex)
            {
                stb.Append(Invariant($"<br><div style='color:Red'>{ex.GetFullMessage()}</div><br>"));
            }
        }

        private IEnumerable<Serie> GetData(string query)
        {
            var loginInformation = pluginConfig.DBLoginInformation;
            return InfluxDBHelper.ExecuteInfluxDBQuery(query, loginInformation).Result;
        }

        private IDictionary<string, FormattableString> GetDefaultValueQueries(DevicePersistenceData data)
        {
            List<string> fields = GetFields(data);

            var queries = new Dictionary<string, FormattableString>()
            {
                {
                    "Last 100 stored values",
                    $"SELECT {string.Join(",", fields)} from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100"
                },
            };

            if (!string.IsNullOrWhiteSpace(data.Field))
            {
                var standardFields = Invariant($"MIN(\"{data.Field}\"), MAX(\"{data.Field}\"), MEAN(\"{data.Field}\"), MEDIAN(\"{data.Field}\"), PERCENTILE(\"{data.Field}\", 95) AS \"95 percentile\"");
                var subQuery24h = Invariant($"SELECT MEAN(\"{data.Field}\") as \"{data.Field}\" FROM \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' AND time > now() - 24h GROUP BY time(1s) fill(previous)");
                queries.Add(
                     "Min/Max Values",
                     $"SELECT MIN(\"{data.Field}\"), MAX(\"{data.Field}\") from \"{data.Measurement}\" WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}'"
                );

                queries.Add(
                      "Min/Max/Average/Medium/Percentile Values(24h)",
                      $"SELECT {standardFields} FROM ({subQuery24h})"
                 );

                queries.Add(
                      "Min/Max/Average/Medium/Percentile Values By Hour(24h)",
                      $"SELECT {standardFields} FROM ({subQuery24h}) GROUP BY time(1h)"
                );
            }
            return queries;
        }

        private static List<string> GetFields(DevicePersistenceData data)
        {
            List<string> fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(data.Field))
            {
                fields.Add(Invariant($"\"{data.Field}\""));
            }

            if (!string.IsNullOrWhiteSpace(data.FieldString))
            {
                fields.Add(Invariant($"\"{data.FieldString}\""));
            }

            return fields;
        }

        private void HandleHistoryPagePostBack(NameValueCollection parts, string form)
        {
            string finalQuery = null;
            if (form == NameToIdWithPrefix(HistoryQueryTypeId))
            {
                var queryType = parts[HistoryQueryTypeId];
                var id = parts[RecordId];
                var data = pluginConfig.DevicePersistenceData[id];
                var queries = GetDefaultValueQueries(data);

                finalQuery = Invariant(queries[queryType]);
                this.divToUpdate.Add(QueryTestDivId, TextArea(QueryTestId, finalQuery));
            }
            else if (form == NameToIdWithPrefix(HistoryRunQueryButtonName))
            {
                finalQuery = parts[QueryTestId];
            }

            StringBuilder stb = new StringBuilder();
            BuildTable(finalQuery, stb, 25);
            this.divToUpdate.Add(HistoryResultDivId, stb.ToString());
        }

        private string HistoryBackButton()
        {
            var b = new clsJQuery.jqButton("Back", "Back", PageName, false)
            {
                id = NameToIdWithPrefix("Back"),
                url = Invariant($"/{pageUrl}?{TabId}=1"),
            };

            return b.Build();
        }

        private void IncludeResourceCSS(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<style type=\"text/css\">");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</style>");
            this.AddScript(stb.ToString());
        }

        private void IncludeResourceScript(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</script>");
            this.AddScript(stb.ToString());
        }

        private const string HistoryQueryTypeId = "historyquerytypeid";
        private const string HistoryResultDivId = "historyresultdivid";
        private const string HistoryRunQueryButtonName = "historyrunquery";
        private const string QueryPartId = "querypart";
        private const string QueryTestDivId = "querytestdivid";
        private const string QueryTestId = "querytextid";
    }
}