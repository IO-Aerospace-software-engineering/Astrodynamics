<?xml version="1.0" encoding="utf-8"?>

<!--********************************************************************--> 
<!-- NOTE: This is version 1.1 of the CDM/XML XSLT (03/02/2021).        -->
<!-- It is designed for use with XML CDMs having qualified elements.    --> 
<!--                                                                    --> 
<!-- Compatible document versions are:                                  --> 
<!-- CDM 508.0-B-1 Blue Book (06/2013)                                  -->
<!--                                                                    --> 
<!-- This style sheet will produce a CDM in the KVN format from an      -->
<!-- input CDM in the XML format that has elements qualified with       -->
<!-- respect to the urn:ccsds:schema:ndmxml namespace.                  -->
<!--                                                                    --> 
<!--********************************************************************--> 

<xsl:stylesheet version="1.0"
xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:msxsl="urn:schemas-microsoft-com:xslt" 
xmlns:ndm="urn:ccsds:schema:ndmxml"
exclude-result-prefixes="msxsl">

<xsl:output method="text" indent="yes"/> 

<xsl:strip-space elements="*"/>
<xsl:preserve-space elements="cdm segment"/> 

<xsl:template match="cdm[@id]">
<xsl:value-of select="@id"/> = <xsl:value-of select="@version"/>
<xsl:apply-templates/>
</xsl:template>

<xsl:template match="ndm:header/ndm:COMMENT">&#10;COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:header/ndm:CREATION_DATE">
CREATION_DATE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:header/ndm:ORIGINATOR">
ORIGINATOR = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:header/ndm:MESSAGE_FOR">
MESSAGE_FOR = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:header/ndm:MESSAGE_ID">
MESSAGE_ID = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:relativeMetadataData/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:TCA">
TCA = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:MISS_DISTANCE[@units]">
MISS_DISTANCE = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:RELATIVE_SPEED">
RELATIVE_SPEED = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_POSITION_R">
RELATIVE_POSITION_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_POSITION_T">
RELATIVE_POSITION_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_POSITION_N">
RELATIVE_POSITION_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>] 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_VELOCITY_R">
RELATIVE_VELOCITY_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_VELOCITY_T">
RELATIVE_VELOCITY_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:relativeStateVector/ndm:RELATIVE_VELOCITY_N">
RELATIVE_VELOCITY_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:START_SCREEN_PERIOD">
START_SCREEN_PERIOD = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:STOP_SCREEN_PERIOD">
STOP_SCREEN_PERIOD = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_VOLUME_FRAME">
SCREEN_VOLUME_FRAME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_VOLUME_SHAPE">
SCREEN_VOLUME_SHAPE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_VOLUME_X">
SCREEN_VOLUME_X = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_VOLUME_Y">
SCREEN_VOLUME_Y = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_VOLUME_Z">
SCREEN_VOLUME_Z = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

<xsl:template  match="ndm:relativeMetadataData/ndm:SCREEN_ENTRY_TIME">
SCREEN_ENTRY_TIME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:SCREEN_EXIT_TIME">
SCREEN_EXIT_TIME = <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:COLLISION_PROBABILITY">
COLLISION_PROBABILITY = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:relativeMetadataData/ndm:COLLISION_PROBABILITY_METHOD">
COLLISION_PROBABILITY_METHOD = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:segment/ndm:metadata/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OBJECT">
OBJECT = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OBJECT_DESIGNATOR">
OBJECT_DESIGNATOR = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:CATALOG_NAME">
CATALOG_NAME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OBJECT_NAME">
OBJECT_NAME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:INTERNATIONAL_DESIGNATOR">
INTERNATIONAL_DESIGNATOR = <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OBJECT_TYPE">
OBJECT_TYPE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OPERATOR_CONTACT_POSITION">
OPERATOR_CONTACT_POSITION = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OPERATOR_ORGANIZATION">
OPERATOR_ORGANIZATION = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OPERATOR_PHONE">
OPERATOR_PHONE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:OPERATOR_EMAIL">
OPERATOR_EMAIL = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:EPHEMERIS_NAME">
EPHEMERIS_NAME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:COVARIANCE_METHOD">
COVARIANCE_METHOD = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:segment/ndm:metadata/ndm:MANEUVERABLE">
MANEUVERABLE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:ORBIT_CENTER">
ORBIT_CENTER = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:REF_FRAME">
REF_FRAME = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:GRAVITY_MODEL">
GRAVITY_MODEL = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:ATMOSPHERIC_MODEL">
ATMOSPHERIC_MODEL = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:N_BODY_PERTURBATIONS">
N_BODY_PERTURBATIONS = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:SOLAR_RAD_PRESSURE">
SOLAR_RAD_PRESSURE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:EARTH_TIDES">
EARTH_TIDES = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:metadata/ndm:INTRACK_THRUST">
INTRACK_THRUST = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:TIME_LASTOB_START">
TIME_LASTOB_START = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:TIME_LASTOB_END">
TIME_LASTOB_END = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:RECOMMENDED_OD_SPAN">
RECOMMENDED_OD_SPAN = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:ACTUAL_OD_SPAN">
ACTUAL_OD_SPAN = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:OBS_AVAILABLE">
OBS_AVAILABLE = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:OBS_USED">
OBS_USED = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:TRACKS_AVAILABLE">
TRACKS_AVAILABLE = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:TRACKS_USED">
TRACKS_USED = <xsl:value-of select="."/> 
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:RESIDUALS_ACCEPTED">
RESIDUALS_ACCEPTED = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:odParameters/ndm:WEIGHTED_RMS">
WEIGHTED_RMS = <xsl:value-of select="."/> 
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:AREA_PC">
AREA_PC = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:AREA_DRG">
AREA_DRG = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:AREA_SRP">
AREA_SRP = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:MASS">
MASS = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:CD_AREA_OVER_MASS">
CD_AREA_OVER_MASS = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:CR_AREA_OVER_MASS">
CR_AREA_OVER_MASS = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:THRUST_ACCELERATION">
THRUST_ACCELERATION = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:additionalParameters/ndm:SEDR">
SEDR = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:X">
X = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:Y">
Y = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:Z">
Z = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:X_DOT">
X_DOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:Y_DOT">
Y_DOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:stateVector/ndm:Z_DOT">
Z_DOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:COMMENT">
COMMENT <xsl:value-of select="."/>
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CR_R">
CR_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CT_R">
CT_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CT_T">
CT_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CN_R">
CN_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CN_T">
CN_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CN_N">
CN_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CRDOT_R">
CRDOT_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CRDOT_T">
CRDOT_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CRDOT_N">
CRDOT_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CRDOT_RDOT">
CRDOT_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTDOT_R">
CTDOT_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTDOT_T">
CTDOT_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTDOT_N">
CTDOT_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTDOT_RDOT">
CTDOT_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTDOT_TDOT">
CTDOT_TDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_R">
CNDOT_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_T">
CNDOT_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_N">
CNDOT_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_RDOT">
CNDOT_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_TDOT">
CNDOT_TDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CNDOT_NDOT">
CNDOT_NDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_R">
CDRG_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_T">
CDRG_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_N">
CDRG_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_RDOT">
CDRG_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_TDOT">
CDRG_TDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_NDOT">
CDRG_NDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CDRG_DRG">
CDRG_DRG = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_R">
CSRP_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_T">
CSRP_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_N">
CSRP_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_RDOT">
CSRP_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_TDOT">
CSRP_TDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_NDOT">
CSRP_NDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_DRG">
CSRP_DRG = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CSRP_SRP">
CSRP_SRP = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_R">
CTHR_R = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_T">
CTHR_T = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_N">
CTHR_N = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_RDOT">
CTHR_RDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_TDOT">
CTHR_TDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_NDOT">
CTHR_NDOT = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_DRG">
CTHR_DRG = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_SRP">
CTHR_SRP = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template>

<xsl:template match="ndm:segment/ndm:data/ndm:covarianceMatrix/ndm:CTHR_THR">
CTHR_THR = <xsl:value-of select="."/> [<xsl:value-of select="@units"/>]
</xsl:template> 

</xsl:stylesheet> 
