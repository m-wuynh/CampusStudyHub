<mxfile host="app.diagrams.net" agent="Gemini-Redesigned">
  <diagram id="pdf-upload-modern" name="Upload PDF Swimlane">
    <mxGraphModel dx="1420" dy="980" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1650" pageHeight="1050" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        
        <!-- TITLE BANNER -->
        <mxCell id="title" parent="1" style="rounded=1;arcSize=8;html=1;align=center;verticalAlign=middle;fillColor=#1e293b;strokeColor=#0f172a;fontColor=#f8fafc;fontSize=22;fontStyle=1;fontFamily=Segoe UI, Arial;" value="LUỒNG XỬ LÝ UPLOAD PDF — SWIMLANE DIAGRAM" vertex="1">
          <mxGeometry height="56" width="1550" x="40" y="20" as="geometry" />
        </mxCell>

        <!-- MAIN POOL -->
        <mxCell id="pool" parent="1" style="swimlane;html=1;horizontal=1;startSize=0;container=1;collapsible=0;fillColor=#ffffff;strokeColor=#cbd5e1;strokeWidth=2;" value="" vertex="1">
          <mxGeometry height="920" width="1550" x="40" y="86" as="geometry" />
        </mxCell>

        <!-- LANE 0: NGƯỜI DÙNG -->
        <mxCell id="lane0" parent="pool" style="swimlane;html=1;horizontal=1;startSize=48;container=1;collapsible=0;fillColor=#dbeafe;swimlaneFillColor=#f8fafc;strokeColor=#93c5fd;strokeWidth=1.5;fontColor=#1e3a8a;fontSize=15;fontStyle=1;fontFamily=Segoe UI, Arial;" value="1. NGƯỜI DÙNG" vertex="1">
          <mxGeometry height="920" width="310" x="0" y="0" as="geometry" />
        </mxCell>
        <mxCell id="begin" parent="lane0" style="ellipse;fillColor=#0f172a;strokeColor=#0f172a;" value="" vertex="1">
          <mxGeometry height="28" width="28" x="141" y="70" as="geometry" />
        </mxCell>
        <mxCell id="select" parent="lane0" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#3b82f6;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontStyle=1;fontFamily=Segoe UI, Arial;shadow=1;" value="Chọn file PDF &amp;&#xa;nhập tiêu đề" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="130" as="geometry" />
        </mxCell>
        <mxCell id="size-end" parent="lane0" style="ellipse;shape=doubleEllipse;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fee2e2;strokeColor=#ef4444;strokeWidth=2;fontColor=#991b1b;fontSize=12;fontStyle=1;fontFamily=Segoe UI, Arial;" value="KẾT THÚC" vertex="1">
          <mxGeometry height="64" width="64" x="123" y="255" as="geometry" />
        </mxCell>
        <mxCell id="duplicate-end" parent="lane0" style="ellipse;shape=doubleEllipse;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fee2e2;strokeColor=#ef4444;strokeWidth=2;fontColor=#991b1b;fontSize=12;fontStyle=1;fontFamily=Segoe UI, Arial;" value="KẾT THÚC" vertex="1">
          <mxGeometry height="64" width="64" x="123" y="385" as="geometry" />
        </mxCell>
        <mxCell id="final-quota-end" parent="lane0" style="ellipse;shape=doubleEllipse;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fee2e2;strokeColor=#ef4444;strokeWidth=2;fontColor=#991b1b;fontSize=12;fontStyle=1;fontFamily=Segoe UI, Arial;" value="KẾT THÚC" vertex="1">
          <mxGeometry height="64" width="64" x="123" y="695" as="geometry" />
        </mxCell>
        <mxCell id="success-end" parent="lane0" style="ellipse;shape=doubleEllipse;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#dcfce7;strokeColor=#16a34a;strokeWidth=2;fontColor=#166534;fontSize=12;fontStyle=1;fontFamily=Segoe UI, Arial;" value="HOÀN TẤT" vertex="1">
          <mxGeometry height="64" width="64" x="123" y="815" as="geometry" />
        </mxCell>

        <!-- LANE 1: GIAO DIỆN -->
        <mxCell id="lane1" parent="pool" style="swimlane;html=1;horizontal=1;startSize=48;container=1;collapsible=0;fillColor=#e0e7ff;swimlaneFillColor=#ffffff;strokeColor=#a5b4fc;strokeWidth=1.5;fontColor=#312e81;fontSize=15;fontStyle=1;fontFamily=Segoe UI, Arial;" value="2. GIAO DIỆN (UI)" vertex="1">
          <mxGeometry height="920" width="310" x="310" y="0" as="geometry" />
        </mxCell>
        <mxCell id="form" parent="lane1" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#6366f1;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="Gửi POST&#xa;multipart/form-data" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="130" as="geometry" />
        </mxCell>
        <mxCell id="size-ui" parent="lane1" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Hiển thị thông báo&#xa;lỗi kích thước file" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="255" as="geometry" />
        </mxCell>
        <mxCell id="duplicate-ui" parent="lane1" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Báo lỗi trùng tên&#xa;hoặc hết quota" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="385" as="geometry" />
        </mxCell>
        <mxCell id="final-quota-ui" parent="lane1" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Hiển thị lỗi&#xa;vượt quá quota trang" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="695" as="geometry" />
        </mxCell>
        <mxCell id="reader" parent="lane1" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#f0fdf4;strokeColor=#16a34a;strokeWidth=1.5;fontColor=#166534;fontSize=14;fontStyle=1;fontFamily=Segoe UI, Arial;shadow=1;" value="Mở trang Reader&#xa;&amp; hiển thị PDF" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="815" as="geometry" />
        </mxCell>

        <!-- LANE 2: CONTROLLER -->
        <mxCell id="lane2" parent="pool" style="swimlane;html=1;horizontal=1;startSize=48;container=1;collapsible=0;fillColor=#ccfbf1;swimlaneFillColor=#f8fafc;strokeColor=#5eead4;strokeWidth=1.5;fontColor=#115e59;fontSize=15;fontStyle=1;fontFamily=Segoe UI, Arial;" value="3. CONTROLLER" vertex="1">
          <mxGeometry height="920" width="310" x="620" y="0" as="geometry" />
        </mxCell>
        <mxCell id="check-size" parent="lane2" style="rhombus;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef9c3;strokeColor=#ca8a04;strokeWidth=1.5;fontColor=#854d0e;fontSize=13;fontStyle=1;fontFamily=Segoe UI, Arial;" value="File rỗng&#xa;hoặc &gt; 25 MiB?" vertex="1">
          <mxGeometry height="92" width="190" x="60" y="116" as="geometry" />
        </mxCell>
        <mxCell id="size-error" parent="lane2" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Gán TempData lỗi;&#xa;Redirect về Library" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="255" as="geometry" />
        </mxCell>
        <mxCell id="duplicate-error" parent="lane2" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Gán TempData lý do;&#xa;Redirect về Library" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="385" as="geometry" />
        </mxCell>
        <mxCell id="final-quota-error" parent="lane2" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef2f2;strokeColor=#ef4444;strokeWidth=1.5;fontColor=#991b1b;fontSize=14;fontFamily=Segoe UI, Arial;" value="Gán TempData lỗi quota;&#xa;Redirect về Library" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="695" as="geometry" />
        </mxCell>
        <mxCell id="redirect" parent="lane2" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#f0fdf4;strokeColor=#16a34a;strokeWidth=1.5;fontColor=#166534;fontSize=14;fontFamily=Segoe UI, Arial;" value="Redirect đến&#xa;Action Reader" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="815" as="geometry" />
        </mxCell>

        <!-- LANE 3: PDF SERVICE -->
        <mxCell id="lane3" parent="pool" style="swimlane;html=1;horizontal=1;startSize=48;container=1;collapsible=0;fillColor=#ffedd5;swimlaneFillColor=#ffffff;strokeColor=#fdba74;strokeWidth=1.5;fontColor=#9a3412;fontSize=15;fontStyle=1;fontFamily=Segoe UI, Arial;" value="4. PDF SERVICE" vertex="1">
          <mxGeometry height="920" width="310" x="930" y="0" as="geometry" />
        </mxCell>
        <mxCell id="precheck" parent="lane3" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#f97316;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="Kiểm tra trùng tên &amp;&#xa;quota tài khoản" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="130" as="geometry" />
        </mxCell>
        <mxCell id="duplicate" parent="lane3" style="rhombus;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef9c3;strokeColor=#ca8a04;strokeWidth=1.5;fontColor=#854d0e;fontSize=13;fontStyle=1;fontFamily=Segoe UI, Arial;" value="Trùng tên hoặc&#xa;hết quota?" vertex="1">
          <mxGeometry height="92" width="190" x="60" y="241" as="geometry" />
        </mxCell>
        <mxCell id="inspect" parent="lane3" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#f97316;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="Đọc cấu trúc PDF &amp;&#xa;đếm tổng số trang" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="485" as="geometry" />
        </mxCell>
        <mxCell id="recheck" parent="lane3" style="rhombus;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#fef9c3;strokeColor=#ca8a04;strokeWidth=1.5;fontColor=#854d0e;fontSize=13;fontStyle=1;fontFamily=Segoe UI, Arial;" value="Đủ quota khi&#xa;cộng file mới?" vertex="1">
          <mxGeometry height="92" width="190" x="60" y="585" as="geometry" />
        </mxCell>
        <mxCell id="complete" parent="lane3" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#f0fdf4;strokeColor=#16a34a;strokeWidth=1.5;fontColor=#166534;fontSize=14;fontStyle=1;fontFamily=Segoe UI, Arial;" value="Xác nhận Upload&#xa;thành công" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="815" as="geometry" />
        </mxCell>

        <!-- LANE 4: FILE / DATABASE -->
        <mxCell id="lane4" parent="pool" style="swimlane;html=1;horizontal=1;startSize=48;container=1;collapsible=0;fillColor=#f1f5f9;swimlaneFillColor=#f8fafc;strokeColor=#cbd5e1;strokeWidth=1.5;fontColor=#334155;fontSize=15;fontStyle=1;fontFamily=Segoe UI, Arial;" value="5. FILE / DATABASE" vertex="1">
          <mxGeometry height="920" width="310" x="1240" y="0" as="geometry" />
        </mxCell>
        <mxCell id="lookup" parent="lane4" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#64748b;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="Tra cứu tên, đếm số PDF&#xa;&amp; tính tổng dung lượng" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="130" as="geometry" />
        </mxCell>
        <mxCell id="save-file" parent="lane4" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#64748b;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="Kiểm tra header %PDF-&#xa;&amp; lưu file vào ổ đĩa" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="385" as="geometry" />
        </mxCell>
        <mxCell id="insert" parent="lane4" style="rounded=1;arcSize=14;whiteSpace=wrap;html=1;align=center;verticalAlign=middle;fillColor=#ffffff;strokeColor=#64748b;strokeWidth=1.5;fontColor=#1e293b;fontSize=14;fontFamily=Segoe UI, Arial;shadow=1;" value="INSERT bản ghi&#xa;PdfDocument vào DB" vertex="1">
          <mxGeometry height="64" width="230" x="40" y="695" as="geometry" />
        </mxCell>

        <!-- CONNECTIONS (CLEAN ORTHOGONAL EDGES) -->
        <mxCell id="e0" edge="1" parent="pool" source="begin" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;" target="select">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e1" edge="1" parent="pool" source="select" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;" target="form">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e2" edge="1" parent="pool" source="form" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;" target="check-size">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <!-- Branch: Check Size -> CÓ (Error) -->
        <mxCell id="e-size-yes" edge="1" parent="pool" source="check-size" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;fontColor=#dc2626;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;" target="size-error" value="CÓ">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e-size-a" edge="1" parent="pool" source="size-error" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="size-ui">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e-size-b" edge="1" parent="pool" source="size-ui" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="size-end">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <!-- Branch: Check Size -> KHÔNG -->
        <mxCell id="e-size-no" edge="1" parent="pool" source="check-size" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;fontColor=#16a34a;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;" target="precheck" value="KHÔNG">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e3" edge="1" parent="pool" source="precheck" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;" target="lookup">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e4" edge="1" parent="pool" source="lookup" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;exitX=0.5;exitY=1;entryX=0.5;entryY=0;" target="duplicate">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1395" y="215" />
              <mxPoint x="1085" y="215" />
            </Array>
          </mxGeometry>
        </mxCell>

        <!-- Branch: Duplicate -> CÓ (Error) -->
        <mxCell id="e-dup-yes" edge="1" parent="pool" source="duplicate" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;fontColor=#dc2626;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;exitX=0;exitY=0.5;entryX=0.5;entryY=0;" target="duplicate-error" value="CÓ">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="775" y="287" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e-dup-a" edge="1" parent="pool" source="duplicate-error" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="duplicate-ui">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e-dup-b" edge="1" parent="pool" source="duplicate-ui" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="duplicate-end">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <!-- Branch: Duplicate -> KHÔNG -->
        <mxCell id="e-dup-no" edge="1" parent="pool" source="duplicate" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;fontColor=#16a34a;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;exitX=1;exitY=0.5;entryX=0.5;entryY=0;" target="save-file" value="KHÔNG">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1395" y="287" />
            </Array>
          </mxGeometry>
        </mxCell>

        <mxCell id="e5" edge="1" parent="pool" source="save-file" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;exitX=0.5;exitY=1;entryX=1;entryY=0.5;" target="inspect">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1395" y="517" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e6" edge="1" parent="pool" source="inspect" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#475569;strokeWidth=1.8;" target="recheck">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <!-- Branch: Recheck -> ĐỦ -->
        <mxCell id="e6b" edge="1" parent="pool" source="recheck" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;fontColor=#16a34a;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;exitX=1;exitY=0.5;entryX=0.5;entryY=0;" target="insert" value="ĐỦ">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1395" y="631" />
            </Array>
          </mxGeometry>
        </mxCell>

        <!-- Branch: Recheck -> VƯỢT (Error) -->
        <mxCell id="e6c" edge="1" parent="pool" source="recheck" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;fontColor=#dc2626;fontSize=12;fontStyle=1;labelBackgroundColor=#ffffff;exitX=0;exitY=0.5;entryX=0.5;entryY=0;" target="final-quota-error" value="VƯỢT">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="775" y="631" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e6d" edge="1" parent="pool" source="final-quota-error" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="final-quota-ui">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e6e" edge="1" parent="pool" source="final-quota-ui" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#dc2626;strokeWidth=1.8;" target="final-quota-end">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <!-- Happy Path Ending -->
        <mxCell id="e7" edge="1" parent="pool" source="insert" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;exitX=0.5;exitY=1;entryX=1;entryY=0.5;" target="complete">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1395" y="847" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e8" edge="1" parent="pool" source="complete" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;" target="redirect">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e9" edge="1" parent="pool" source="redirect" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;" target="reader">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e10" edge="1" parent="pool" source="reader" style="edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#16a34a;strokeWidth=1.8;" target="success-end">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>