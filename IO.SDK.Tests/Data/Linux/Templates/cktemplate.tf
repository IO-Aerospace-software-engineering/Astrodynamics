KPL/FK

\begindata
      
 
      FRAME_{framename}_SPACECRAFT   = {frameid}
      FRAME_{frameid}_NAME      = '{framename}_SPACECRAFT'
      FRAME_{frameid}_CLASS     =  3
      FRAME_{frameid}_CLASS_ID  = {frameid}
      FRAME_{frameid}_CENTER    = {spacecraftid}
 
      CK_{frameid}_SCLK         = {spacecraftid}
      CK_{frameid}_SPK          = {spacecraftid}
 
      OBJECT_{spacecraftid}_FRAME       = '{framename}_SPACECRAFT'

      NAIF_BODY_NAME              += '{framename}_SPACECRAFT'
      NAIF_BODY_CODE              += {frameid}
      
      NAIF_BODY_NAME              += '{spacecraftname}'
      NAIF_BODY_CODE              += {spacecraftid}

\begintext
