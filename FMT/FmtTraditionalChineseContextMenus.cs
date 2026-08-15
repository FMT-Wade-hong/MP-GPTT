using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    /// <summary>
    /// FMT's Traditional Chinese context-menu vocabulary. The upstream zh-Hant
    /// resources are incomplete and contain a number of Simplified Chinese values,
    /// so the customized desktop UI applies this table after InitializeComponent.
    /// </summary>
    internal static class FmtTraditionalChineseContextMenus
    {
        private static readonly Dictionary<string, string> TextByName =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Flight Data: HUD, quick views, map and gimbal video.
                {"videoToolStripMenuItem", "影像"},
                {"recordHudToAVIToolStripMenuItem", "將 HUD 錄製為 AVI"},
                {"stopRecordToolStripMenuItem", "停止錄影"},
                {"setMJPEGSourceToolStripMenuItem", "設定 MJPEG 來源"},
                {"startCameraToolStripMenuItem", "啟動相機"},
                {"setGStreamerSourceToolStripMenuItem", "設定 GStreamer 來源"},
                {"hereLinkVideoToolStripMenuItem", "HereLink 影像"},
                {"gStreamerStopToolStripMenuItem", "停止 GStreamer"},
                {"setAspectRatioToolStripMenuItem", "設定畫面比例"},
                {"userItemsToolStripMenuItem", "自訂顯示項目"},
                {"russianHudToolStripMenuItem", "俄式 HUD"},
                {"swapWithMapToolStripMenuItem", "與地圖交換位置"},
                {"groundColorToolStripMenuItem", "地面顏色"},
                {"setBatteryCellCountToolStripMenuItem", "設定電池單體電壓"},
                {"showIconsToolStripMenuItem", "顯示圖示"},
                {"customizeToolStripMenuItem", "自訂"},
                {"multiLineToolStripMenuItem", "多行顯示"},
                {"setViewCountToolStripMenuItem", "設定資訊格數量"},
                {"undockToolStripMenuItem", "分離視窗"},
                {"goHereToolStripMenuItem", "飛到此處"},
                {"flyToHereAltToolStripMenuItem", "以指定高度飛到此處"},
                {"flyToCoordsToolStripMenuItem", "飛往指定座標"},
                {"addPoiToolStripMenuItem", "新增興趣點"},
                {"deleteToolStripMenuItem", "刪除興趣點"},
                {"saveFileToolStripMenuItem", "儲存興趣點檔案"},
                {"loadFileToolStripMenuItem", "載入興趣點檔案"},
                {"poiatcoordsToolStripMenuItem", "在指定座標新增興趣點"},
                {"pointCameraHereToolStripMenuItem", "鏡頭指向此處"},
                {"PointCameraCoordsToolStripMenuItem1", "鏡頭指向指定座標"},
                {"triggerCameraToolStripMenuItem", "立即觸發拍照"},
                {"flightPlannerToolStripMenuItem", "任務規劃"},
                {"setHomeHereToolStripMenuItem", "將此處設為 HOME"},
                {"setEKFHomeHereToolStripMenuItem", "將此處設為 EKF 原點"},
                {"setHomeHereToolStripMenuItem1", "將此處設為 HOME"},
                {"takeOffToolStripMenuItem", "起飛"},
                {"onOffCameraOverlapToolStripMenuItem", "顯示相機重疊區"},
                {"jumpToTagToolStripMenuItem", "跳轉至任務標籤"},
                {"gimbalVideoToolStripMenuItem", "雲台影像"},
                {"gimbalVideoFullSizedToolStripMenuItem", "完整大小"},
                {"gimbalVideoMiniToolStripMenuItem", "迷你視窗"},
                {"gimbalVideoPopOutToolStripMenuItem", "彈出視窗"},

                // Mission Planning: waypoint and mission commands.
                {"deleteWPToolStripMenuItem", "刪除航點"},
                {"insertWpToolStripMenuItem", "插入航點"},
                {"currentPositionToolStripMenuItem", "插入於目前位置"},
                {"insertSplineWPToolStripMenuItem", "插入曲線航點"},
                {"loiterToolStripMenuItem", "盤旋"},
                {"loiterForeverToolStripMenuItem", "持續盤旋"},
                {"loitertimeToolStripMenuItem", "依時間盤旋"},
                {"loitercirclesToolStripMenuItem", "依圈數盤旋"},
                {"jumpToolStripMenuItem", "跳轉"},
                {"jumpstartToolStripMenuItem", "跳轉至起點"},
                {"jumpwPToolStripMenuItem", "跳轉至航點編號"},
                {"rTLToolStripMenuItem", "返航（RTL）"},
                {"landToolStripMenuItem", "降落"},
                {"takeoffToolStripMenuItem", "起飛"},
                {"setROIToolStripMenuItem", "設定關注區域（ROI）"},
                {"clearMissionToolStripMenuItem", "清除任務"},

                // Polygon, geofence and rally-point tools.
                {"polygonToolStripMenuItem", "多邊形"},
                {"addPolygonPointToolStripMenuItem", "繪製多邊形"},
                {"addPolygonPointToolStripMenuItem2", "繪製多邊形"},
                {"clearPolygonToolStripMenuItem", "清除多邊形"},
                {"clearPolygonToolStripMenuItem2", "清除多邊形"},
                {"savePolygonToolStripMenuItem", "儲存多邊形"},
                {"savePolygonToolStripMenuItem2", "儲存多邊形"},
                {"loadPolygonToolStripMenuItem", "載入多邊形"},
                {"loadPolygonToolStripMenuItem2", "載入多邊形"},
                {"fromSHPToolStripMenuItem", "從 SHP 建立"},
                {"fromSHPToolStripMenuItem2", "從 SHP 建立"},
                {"fromCurrentWaypointsToolStripMenuItem", "從目前航點建立"},
                {"convertWPToPolygonToolStripMenuItem", "從目前航點建立"},
                {"offsetPolygonToolStripMenuItem", "偏移多邊形"},
                {"offsetPolygonToolStripMenuItem2", "偏移多邊形"},
                {"areaToolStripMenuItem", "計算面積"},
                {"areaToolStripMenuItem1", "區域航點"},
                {"areaToolStripMenuItem2", "計算面積"},
                {"geoFenceToolStripMenuItem", "地理圍籬"},
                {"GeoFenceuploadToolStripMenuItem", "上傳至飛控"},
                {"GeoFencedownloadToolStripMenuItem", "從飛控下載"},
                {"setReturnLocationToolStripMenuItem", "設定圍籬返航位置"},
                {"loadFromFileToolStripMenuItem", "從檔案載入"},
                {"saveToFileToolStripMenuItem", "儲存至檔案"},
                {"clearToolStripMenuItem", "清除"},
                {"fenceInclusionToolStripMenuItem", "圍籬納入區"},
                {"fenceExclusionToolStripMenuItem", "圍籬排除區"},
                {"rallyPointsToolStripMenuItem", "集合點"},
                {"setRallyPointToolStripMenuItem", "設定集合點"},
                {"getRallyPointsToolStripMenuItem", "從飛控下載"},
                {"saveRallyPointsToolStripMenuItem", "上傳至飛控"},
                {"clearRallyPointsToolStripMenuItem", "清除集合點"},
                {"saveToFileToolStripMenuItem1", "儲存集合點檔案"},
                {"loadFromFileToolStripMenuItem1", "載入集合點檔案"},

                // Automatic waypoint, map and file tools.
                {"autoWPToolStripMenuItem", "自動建立航點"},
                {"createWpCircleToolStripMenuItem", "建立航點圓形路線"},
                {"createSplineCircleToolStripMenuItem", "建立曲線圓形路線"},
                {"textToolStripMenuItem", "依文字建立航點"},
                {"createCircleSurveyToolStripMenuItem", "建立圓形測繪"},
                {"surveyGridToolStripMenuItem", "網格測繪"},
                {"mapToolToolStripMenuItem", "地圖工具"},
                {"ContextMeasure", "測量距離"},
                {"rotateMapToolStripMenuItem", "旋轉地圖"},
                {"zoomToToolStripMenuItem", "縮放至"},
                {"zoomToVehicleToolStripMenuItem", "縮放至飛行器"},
                {"zoomToMissionToolStripMenuItem", "縮放至任務範圍"},
                {"zoomToHomeToolStripMenuItem", "縮放至 HOME"},
                {"prefetchToolStripMenuItem", "預先下載目前地圖"},
                {"prefetchWPPathToolStripMenuItem", "預先下載航點路徑地圖"},
                {"kMLOverlayToolStripMenuItem", "載入 KML 圖層"},
                {"elevationGraphToolStripMenuItem", "高度與地形曲線"},
                {"reverseWPsToolStripMenuItem", "反轉航點順序"},
                {"fileLoadSaveToolStripMenuItem", "任務檔案"},
                {"loadWPFileToolStripMenuItem", "載入航點檔案"},
                {"loadAndAppendToolStripMenuItem", "載入並附加航點"},
                {"saveWPFileToolStripMenuItem", "儲存航點檔案"},
                {"loadKMLFileToolStripMenuItem", "載入 KML 檔案"},
                {"loadSHPFileToolStripMenuItem", "載入 SHP 檔案"},
                {"pOIToolStripMenuItem", "興趣點（POI）"},
                {"poiaddToolStripMenuItem", "新增"},
                {"poideleteToolStripMenuItem", "刪除"},
                {"poieditToolStripMenuItem", "編輯"},
                {"trackerHomeToolStripMenuItem", "追蹤器 HOME"},
                {"modifyAltToolStripMenuItem", "批次修改高度"},
                {"enterUTMCoordToolStripMenuItem", "輸入 UTM 座標"},
                {"switchDockingToolStripMenuItem", "切換停駐版面"},
                {"drawAPolygonToolStripMenuItem", "繪製多邊形"},
                {"gDALOpacityToolStripMenuItem", "GDAL 圖層透明度"},
                {"testToolStripMenuItem", "測試"}
            };

        internal static void Apply(params ContextMenuStrip[] menus)
        {
            if (menus == null)
                return;

            foreach (var menu in menus)
            {
                if (menu != null)
                    Apply(menu.Items);
            }
        }

        private static void Apply(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                string text;
                if (!string.IsNullOrEmpty(item.Name) && TextByName.TryGetValue(item.Name, out text))
                    item.Text = text;

                var dropDown = item as ToolStripDropDownItem;
                if (dropDown != null && dropDown.HasDropDownItems)
                    Apply(dropDown.DropDownItems);
            }
        }
    }
}
