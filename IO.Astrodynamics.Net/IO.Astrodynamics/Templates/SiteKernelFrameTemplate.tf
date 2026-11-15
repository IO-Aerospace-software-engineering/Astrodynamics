KPL/FK
\begindata
NAIF_BODY_NAME              += '{sitename}'
NAIF_BODY_CODE              += {siteid}
FRAME_{sitenametopo}        = {frameid}
FRAME_{frameid}_NAME        = '{sitenametopo}'
FRAME_{frameid}_CLASS       = 4
FRAME_{frameid}_CLASS_ID    = {frameid}
FRAME_{frameid}_CENTER      = {siteid}
OBJECT_{siteid}_FRAME       = '{sitenametopo}'
TKFRAME_{frameid}_SPEC      = 'MATRIX'
TKFRAME_{frameid}_RELATIVE  = '{fixedframe}'
TKFRAME_{frameid}_MATRIX    = ({matrix})
\begintext
