param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class FmtChineseConverter
{
    private const uint TraditionalChinese = 0x04000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int LCMapStringEx(
        string localeName,
        uint mapFlags,
        string source,
        int sourceLength,
        [Out] char[] destination,
        int destinationLength,
        IntPtr versionInformation,
        IntPtr reserved,
        IntPtr sortHandle);

    public static string ToTraditional(string value)
    {
        if (String.IsNullOrEmpty(value))
            return value;

        int required = LCMapStringEx("zh-TW", TraditionalChinese, value, value.Length,
            null, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (required == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var output = new char[required];
        int written = LCMapStringEx("zh-TW", TraditionalChinese, value, value.Length,
            output, output.Length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (written == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return new string(output, 0, written).TrimEnd('\0');
    }
}
'@

$taiwanTerms = @(
    @('文件夾', '資料夾'),
    @('文件', '檔案'),
    @('軟件', '軟體'),
    @('硬件', '硬體'),
    @('固件', '韌體'),
    @('設置', '設定'),
    @('默認', '預設'),
    @('連接', '連線'),
    @('網絡', '網路'),
    @('鼠標', '滑鼠'),
    @('屏幕', '螢幕'),
    @('打印', '列印'),
    @('視頻', '視訊'),
    @('音頻', '音訊'),
    @('內存', '記憶體'),
    @('數據', '資料'),
    @('信息', '資訊'),
    @('消息', '訊息'),
    @('用戶', '使用者'),
    @('接口', '介面'),
    @('菜單', '選單'),
    @('窗口', '視窗'),
    @('模塊', '模組'),
    @('加載', '載入'),
    @('保存', '儲存'),
    @('創建', '建立'),
    @('添加', '新增'),
    @('查看', '檢視'),
    @('啟動項', '啟動項目'),
    @('檔案名', '檔名'),
    @('日志', '日誌'),
    @('標志', '標誌'),
    @('范圍', '範圍'),
    @('規范', '規範'),
    @('示范', '示範'),
    @('繪制', '繪製'),
    @('復制', '複製'),
    @('制作', '製作'),
    @('制造', '製造'),
    @('復雜', '複雜'),
    @('重復', '重複'),
    @('緩沖', '緩衝'),
    @('沖突', '衝突'),
    @('沖擊', '衝擊'),
    @('聯系', '聯繫'),
    @('位于', '位於'),
    @('由于', '由於'),
    @('然后', '然後'),
    @('之后', '之後'),
    @('以后', '以後'),
    @('最后', '最後'),
    @('這里', '這裡'),
    @('那里', '那裡'),
    @('日誌里', '日誌裡'),
    @('其余', '其餘'),
    @('多余', '多餘'),
    @('剩余', '剩餘'),
    @('余量', '餘量'),
    @('主干', '主幹'),
    @('頭發', '頭髮')
)

function Convert-ToTaiwanTraditional {
    param([string]$Value)

    $result = [FmtChineseConverter]::ToTraditional($Value)
    foreach ($term in $taiwanTerms) {
        $result = $result.Replace($term[0], $term[1])
    }
    return $result
}

function Get-ResourceValues {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($Path)
    foreach ($data in $document.SelectNodes('/root/data')) {
        $valueNode = $data.SelectSingleNode('value')
        if ($null -ne $valueNode) {
            $values[$data.GetAttribute('name')] = $valueNode.InnerText
        }
    }
    return $values
}

function Decode-XmlText {
    param([string]$Value)

    return [System.Net.WebUtility]::HtmlDecode($Value)
}

function Encode-XmlText {
    param([string]$Value)

    return $Value.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

$resourceFiles = Get-ChildItem -LiteralPath $ProjectRoot -Recurse -File |
    Where-Object {
        ($_.Name.EndsWith('.zh-Hans.resx') -or $_.Name.EndsWith('.zh-CN.resx')) -and
        $_.FullName -notmatch '[\\/](bin|obj|obj3)[\\/]'
    }

$dataPattern = [regex]::new(
    '(?<prefix><data\b[^>]*\bname="(?<name>[^"]+)"[^>]*>\s*<value(?:\s+[^>]*)?(?<!/)>)(?<text>.*?)(?<suffix></value>)',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
$hanPattern = [regex]::new('[\u3400-\u4DBF\u4E00-\u9FFF]')

$filesChanged = 0
$valuesChanged = 0
$valuesFromExistingTranslation = 0
$valuesConverted = 0

foreach ($resourceFile in $resourceFiles) {
    if ($resourceFile.Name.EndsWith('.zh-Hans.resx')) {
        $basePath = $resourceFile.FullName.Substring(0,
            $resourceFile.FullName.Length - '.zh-Hans.resx'.Length)
        $counterparts = @(
            ($basePath + '.zh-Hant.resx'),
            ($basePath + '.zh-TW.resx'))
    }
    else {
        $basePath = $resourceFile.FullName.Substring(0,
            $resourceFile.FullName.Length - '.zh-CN.resx'.Length)
        $counterparts = @(
            ($basePath + '.zh-TW.resx'),
            ($basePath + '.zh-Hant.resx'))
    }

    $existingValues = @{}
    foreach ($counterpart in $counterparts) {
        if (Test-Path -LiteralPath $counterpart) {
            $candidateValues = Get-ResourceValues -Path $counterpart
            foreach ($key in $candidateValues.Keys) {
                if (-not $existingValues.ContainsKey($key) -and
                    $hanPattern.IsMatch([string]$candidateValues[$key])) {
                    $existingValues[$key] = $candidateValues[$key]
                }
            }
        }
    }

    $bytes = [System.IO.File]::ReadAllBytes($resourceFile.FullName)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $content = [System.IO.File]::ReadAllText($resourceFile.FullName)
    $script:fileValueChanges = 0

    $converted = $dataPattern.Replace($content,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)

            $originalValue = Decode-XmlText -Value $match.Groups['text'].Value
            if (-not $hanPattern.IsMatch($originalValue)) {
                return $match.Value
            }

            $name = $match.Groups['name'].Value
            if ($existingValues.ContainsKey($name)) {
                $newValue = Convert-ToTaiwanTraditional -Value ([string]$existingValues[$name])
                $script:valuesFromExistingTranslation++
            }
            else {
                $newValue = Convert-ToTaiwanTraditional -Value $originalValue
                $script:valuesConverted++
            }

            if ($newValue -eq $originalValue) {
                return $match.Value
            }

            $script:valuesChanged++
            $script:fileValueChanges++
            $encodedValue = Encode-XmlText -Value $newValue
            return $match.Groups['prefix'].Value + $encodedValue + $match.Groups['suffix'].Value
        })

    if ($fileValueChanges -gt 0) {
        $validationDocument = New-Object System.Xml.XmlDocument
        $validationDocument.PreserveWhitespace = $true
        $validationDocument.LoadXml($converted)

        $encoding = New-Object System.Text.UTF8Encoding($hasUtf8Bom)
        [System.IO.File]::WriteAllText($resourceFile.FullName, $converted, $encoding)
        $filesChanged++
    }
}

[PSCustomObject]@{
    ResourceFiles = $resourceFiles.Count
    FilesChanged = $filesChanged
    ValuesChanged = $valuesChanged
    ExistingTraditionalValuesUsed = $valuesFromExistingTranslation
    MechanicallyConvertedValues = $valuesConverted
}
