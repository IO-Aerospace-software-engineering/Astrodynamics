KPL/FK
\begindata
FRAME_{spacecraftname}_{instrumentname}             = {instrumentid}
FRAME_{instrumentid}_NAME        = '{framename}'
FRAME_{instrumentid}_CLASS       = 4
FRAME_{instrumentid}_CLASS_ID    = {instrumentid}
FRAME_{instrumentid}_CENTER      = {spacecraftid}
TKFRAME_{instrumentid}_SPEC      = 'ANGLES'
TKFRAME_{instrumentid}_RELATIVE  = '{spacecraftframe}'
TKFRAME_{instrumentid}_ANGLES    = ( {x},{y},{z} )
TKFRAME_{instrumentid}_AXES      = ( 1,    2,   3   )
TKFRAME_{instrumentid}_UNITS     = 'RADIANS'
NAIF_BODY_NAME              += '{spacecraftname}_{instrumentname}'
NAIF_BODY_CODE              += {instrumentid}
\begintext
