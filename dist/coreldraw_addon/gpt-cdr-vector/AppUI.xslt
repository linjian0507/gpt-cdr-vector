<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>

  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <itemData guid="96d6b822-8d2c-4077-8f8f-d1ce0b9896e5"
                type="wpfhost"
                hostedType="Addons\gpt-cdr-vector\GptCdrVectorHost.dll,GptCdrVectorHost.Toolbar"
                enable="true">
      </itemData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <commandBarData guid="9f52c93f-d4a5-4317-961d-1d271a0eb211"
                      nonLocalizableName="gpt-cdr-vector"
                      userCaption="GPT CDR Vector"
                      locked="false"
                      type="toolbar">
        <toolbar>
          <item guidRef="96d6b822-8d2c-4077-8f8f-d1ce0b9896e5" dock="top"/>
        </toolbar>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/containers/container[@guid='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']/container[@guid='Framework_MainFrame-layout']/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']">
    <xsl:copy-of select="."/>
    <toolbar guidRef="9f52c93f-d4a5-4317-961d-1d271a0eb211" dock="top" />
  </xsl:template>
</xsl:stylesheet>
