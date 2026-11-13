; Z0Clean v2.2
; Safe Z=0 post-cleaner for 2D as-built drawings.
; Commands:
;   Z0SCAN  - scan selected objects; makes NO changes.
;   Z0CLEAN - scan, show summary, then optionally clean.
;
; Design rules:
; - NEVER EXPLODES objects.
; - NEVER changes DXF 210 Normal / Extrusion direction.
; - Keeps polyline width, bulge, arc angles, block scale/rotation, etc.
; - Objects whose plane is genuinely tilted away from World XY are SKIPPED,
;   because forcing Normal=(0,0,1) without a real geometric projection can
;   corrupt ARC/CIRCLE/TEXT/HATCH/BLOCK and similar OCS-based entities.
; - IMAGE and WIPEOUT are supported when their U/V vectors lie in World XY;
;   only insertion Z is changed. Tilted rasters are skipped.
; - Other complex / unsafe entities (MLEADER, UNDERLAY, OLE, ACIS, proxies,
;   etc.) are reported and skipped instead of being damaged.
;
; IMPORTANT:
; - Work on a COPY first.
; - If "clean inside block definitions" = Yes, EVERY insertion of that block
;   definition is affected.
;
; Tested logic is based on ordinary AutoCAD DXF entities and entmod.
; No EXPLODE, no FLATTEN, no Normal rewrite.

(vl-load-com)

(setq *mrtz0:tol* 1.0e-9)

; -------------------------
; Small helpers
; -------------------------

(defun mrtz0:abs (x) (if (< x 0.0) (- x) x))

(defun mrtz0:pt3-p (v)
  (and (listp v) (>= (length v) 3))
)

(defun mrtz0:nonzero-z-p (v)
  (and (mrtz0:pt3-p v)
       (> (mrtz0:abs (caddr v)) *mrtz0:tol*))
)

(defun mrtz0:zero-z-point (v)
  (if (mrtz0:pt3-p v)
    (list (car v) (cadr v) 0.0)
    v
  )
)

(defun mrtz0:normal (ed)
  (cond
    ((cdr (assoc 210 ed)))
    ('(0.0 0.0 1.0))
  )
)

(defun mrtz0:z-parallel-normal-p (ed / n)
  ; Accept both +Z and -Z. We do NOT rewrite the normal.
  (setq n (mrtz0:normal ed))
  (and
    (< (mrtz0:abs (car n)) *mrtz0:tol*)
    (< (mrtz0:abs (cadr n)) *mrtz0:tol*)
    (> (mrtz0:abs (caddr n)) (- 1.0 *mrtz0:tol*))
  )
)

(defun mrtz0:zero-point-codes (ed codes / out itm gc val)
  ; Rewrites EVERY occurrence of the requested point/vector group codes.
  ; 2D point values are left untouched.
  (setq out '())
  (foreach itm ed
    (setq gc  (car itm)
          val (cdr itm))
    (if (and (member gc codes) (mrtz0:pt3-p val))
      (setq itm (cons gc (mrtz0:zero-z-point val)))
    )
    (setq out (cons itm out))
  )
  (reverse out)
)

(defun mrtz0:zero-scalar-code (ed gc / out itm)
  ; If code exists, set it to 0.0. If absent, do not add it.
  (setq out '())
  (foreach itm ed
    (if (= (car itm) gc)
      (setq itm (cons gc 0.0))
    )
    (setq out (cons itm out))
  )
  (reverse out)
)

(defun mrtz0:code-nonzero-p (ed gc / a)
  (and
    (setq a (assoc gc ed))
    (numberp (cdr a))
    (> (mrtz0:abs (cdr a)) *mrtz0:tol*)
  )
)

(defun mrtz0:point-codes-have-z-p (ed codes / hit itm)
  (setq hit nil)
  (foreach itm ed
    (if (and
          (member (car itm) codes)
          (mrtz0:nonzero-z-p (cdr itm)))
      (setq hit T)
    )
  )
  hit
)

(defun mrtz0:direction-xy-zero-p (ed gc / a v)
  ; Used for RAY/XLINE. Flattening a purely vertical direction would create
  ; a zero direction vector, which is invalid.
  (if (and
        (setq a (assoc gc ed))
        (setq v (cdr a))
        (mrtz0:pt3-p v))
    (and
      (< (mrtz0:abs (car v)) *mrtz0:tol*)
      (< (mrtz0:abs (cadr v)) *mrtz0:tol*)
    )
    nil
  )
)

(defun mrtz0:changed-p (old new)
  (not (equal old new 1.0e-12))
)

; -------------------------
; Result / counting helpers
; -------------------------

; Result shape:
;   ("OK"        newEd)
;   ("NOCHANGE"  oldEd)
;   ("SKIP"      reason oldEd)

(defun mrtz0:res-ok (ed)       (list "OK" ed))
(defun mrtz0:res-nochange (ed) (list "NOCHANGE" ed))
(defun mrtz0:res-skip (why ed) (list "SKIP" why ed))

(defun mrtz0:inc-assoc (key alist / a)
  (if (setq a (assoc key alist))
    (subst (cons key (1+ (cdr a))) a alist)
    (cons (cons key 1) alist)
  )
)

(defun mrtz0:count-type (typ)
  (setq *mrtz0:type-counts* (mrtz0:inc-assoc typ *mrtz0:type-counts*))
)

(defun mrtz0:count-change (typ)
  (setq *mrtz0:change-counts* (mrtz0:inc-assoc typ *mrtz0:change-counts*))
)

(defun mrtz0:count-skip (why)
  (setq *mrtz0:skip-counts* (mrtz0:inc-assoc why *mrtz0:skip-counts*))
)

(defun mrtz0:count-fail (typ)
  (setq *mrtz0:fail-counts* (mrtz0:inc-assoc typ *mrtz0:fail-counts*))
)

(defun mrtz0:reset-counts ()
  (setq
    *mrtz0:type-counts*   '()
    *mrtz0:change-counts* '()
    *mrtz0:skip-counts*   '()
    *mrtz0:fail-counts*   '()
    *mrtz0:seen-blocks*   '()
  )
)

(defun mrtz0:sort-pairs (al)
  (vl-sort al
    '(lambda (a b) (< (strcase (car a)) (strcase (car b)))))
)

(defun mrtz0:print-pairs (title al / p)
  (if al
    (progn
      (princ (strcat "\n" title))
      (foreach p (mrtz0:sort-pairs al)
        (princ
          (strcat
            "\n  "
            (car p)
            ": "
            (itoa (cdr p))
          )
        )
      )
    )
  )
)

(defun mrtz0:print-summary (mode)
  (princ
    (strcat
      "\n\n--- Z0Clean "
      mode
      " SUMMARY ---"
    )
  )
  (mrtz0:print-pairs "Selected/scanned entity types:" *mrtz0:type-counts*)
  (mrtz0:print-pairs "Entities needing Z cleanup:" *mrtz0:change-counts*)
  (mrtz0:print-pairs "Skipped for safety:" *mrtz0:skip-counts*)
  (mrtz0:print-pairs "Failed/locked during write:" *mrtz0:fail-counts*)
  (princ "\n-------------------------")
)

; -------------------------
; Raster IMAGE / WIPEOUT helpers
; -------------------------

(defun mrtz0:raster-vector-horizontal-p (ed gc / a v)
  ; IMAGE/WIPEOUT groups 11 and 12 are WCS U/V vectors.
  (if (and
        (setq a (assoc gc ed))
        (setq v (cdr a))
        (mrtz0:pt3-p v))
    (< (mrtz0:abs (caddr v)) *mrtz0:tol*)
    T
  )
)

(defun mrtz0:raster-horizontal-p (ed)
  (and
    (mrtz0:raster-vector-horizontal-p ed 11)
    (mrtz0:raster-vector-horizontal-p ed 12)
  )
)

(defun mrtz0:plan-raster (ed / typ newed)
  ; AcDbRasterImage geometry:
  ;   10 = insertion point, WCS 3D
  ;   11 = U vector, WCS 3D
  ;   12 = V vector, WCS 3D
  ;   13 = image size, 2D              -> do not touch
  ;   14 = clipping vertices, 2D       -> do not touch
  ;
  ; If U/V are already horizontal, cleaning insertion Z is safe.
  ; If U/V contain Z, the raster is genuinely tilted and is skipped.
  (setq typ (cdr (assoc 0 ed)))
  (if (not (mrtz0:raster-horizontal-p ed))
    (mrtz0:res-skip (strcat typ " - tilted raster U/V vectors") ed)
    (progn
      (setq newed (mrtz0:zero-point-codes ed '(10)))
      (if (mrtz0:changed-p ed newed)
        (mrtz0:res-ok newed)
        (mrtz0:res-nochange ed)
      )
    )
  )
)

; -------------------------
; Tilted LWPOLYLINE -> exact XY projection (line-only)
; -------------------------
; A tilted LWPOLYLINE stores vertices in its OCS. Simply setting Elevation=0
; can move it far in WCS. For line-only polylines, we can safely:
;   OCS vertex -> WCS -> set WCS Z=0 -> write back as World-XY LWPOLYLINE.
; This preserves the entity as ONE LWPOLYLINE and keeps widths.
; If any bulge != 0, projection of the circular arc would generally become
; an ellipse, so the entity is skipped rather than silently distorted.

(defun mrtz0:lwpoly-has-bulge-p (ed / itm hit)
  (setq hit nil)
  (foreach itm ed
    (if (and
          (= (car itm) 42)
          (numberp (cdr itm))
          (> (mrtz0:abs (cdr itm)) *mrtz0:tol*))
      (setq hit T)
    )
  )
  hit
)

(defun mrtz0:lwpoly-elevation (ed / a)
  (if (setq a (assoc 38 ed))
    (cdr a)
    0.0
  )
)

(defun mrtz0:project-lwpoly-to-world-xy (en ed / elev out itm gc v p3 w)
  (setq elev (mrtz0:lwpoly-elevation ed)
        out '())

  (foreach itm ed
    (setq gc (car itm)
          v  (cdr itm))
    (cond
      ((= gc 10)
       ; LWPOLYLINE vertex is OCS XY. Combine it with common elevation,
       ; transform through the entity OCS into WCS, then project to Z=0.
       (setq p3 (list (car v) (cadr v) elev))
       (setq w  (trans p3 en 0))
       ; Under the new World-Z normal, group 10 is simply WCS XY.
       (setq itm (cons 10 (list (car w) (cadr w))))
      )
      ((= gc 38)
       (setq itm (cons 38 0.0))
      )
      ((= gc 39)
       ; Remove thickness for a true 2D result.
       (setq itm (cons 39 0.0))
      )
      ((= gc 210)
       ; Safe here ONLY because every OCS vertex above has already been
       ; transformed into WCS before the normal is changed.
       (setq itm (cons 210 '(0.0 0.0 1.0)))
      )
    )
    (setq out (cons itm out))
  )

  ; Some LWPOLYLINEs omit 38/210 when default. Add them explicitly so the
  ; resulting entity is unambiguous in Properties/LIST.
  (setq out (reverse out))
  (if (null (assoc 38 out))
    (setq out (append out (list (cons 38 0.0))))
  )
  (if (null (assoc 210 out))
    (setq out (append out (list (cons 210 '(0.0 0.0 1.0)))))
  )
  out
)

(defun mrtz0:plan-lwpoly (en ed / newed)
  (cond
    ; Normal already parallel to World Z: ordinary safe elevation cleanup.
    ((mrtz0:z-parallel-normal-p ed)
     (setq newed (mrtz0:zero-scalar-code ed 38))
     (setq newed (mrtz0:zero-scalar-code newed 39))
     (if (mrtz0:changed-p ed newed)
       (mrtz0:res-ok newed)
       (mrtz0:res-nochange ed)
     )
    )

    ; Tilted + circular arc/bulge: exact plan projection is not representable
    ; as the same circular-bulge LWPOLYLINE, so do not corrupt it.
    ((mrtz0:lwpoly-has-bulge-p ed)
     (mrtz0:res-skip
       "LWPOLYLINE - tilted with bulge; exact XY projection would not stay circular"
       ed
     )
    )

    ; Tilted + straight segments only: exact vertex projection is safe.
    (T
     (setq newed (mrtz0:project-lwpoly-to-world-xy en ed))
     (if (mrtz0:changed-p ed newed)
       (mrtz0:res-ok newed)
       (mrtz0:res-nochange ed)
     )
    )
  )
)

; -------------------------
; Raw entity planner
; -------------------------

(defun mrtz0:plan-raw (ed / typ newed)
  ; Returns a result without writing anything.
  (setq typ (cdr (assoc 0 ed))
        newed ed)

  (cond

    ; ----- WCS / directly flattenable geometry -----

    ((= typ "LINE")
     (setq newed (mrtz0:zero-point-codes newed '(10 11)))
     (setq newed (mrtz0:zero-scalar-code newed 39))
    )

    ((= typ "POINT")
     (setq newed (mrtz0:zero-point-codes newed '(10)))
    )

    ((= typ "3DFACE")
     (setq newed (mrtz0:zero-point-codes newed '(10 11 12 13)))
    )

    ((= typ "SPLINE")
     ; 10 = control points, 11 = fit points,
     ; 12/13 = start/end tangents (vectors).
     (setq newed (mrtz0:zero-point-codes newed '(10 11 12 13)))
    )

    ((or (= typ "XLINE") (= typ "RAY"))
     (if (mrtz0:direction-xy-zero-p ed 11)
       (setq newed 'VERTICAL)
       (setq newed (mrtz0:zero-point-codes newed '(10 11)))
     )
    )

    ((= typ "LEADER")
     ; Leader vertices are repeated group 10 points.
     (setq newed (mrtz0:zero-point-codes newed '(10)))
    )

    ((= typ "MLINE")
     ; 11 = vertices, 12/13 = direction/miter vectors.
     (setq newed (mrtz0:zero-point-codes newed '(11 12 13)))
    )

    ((= typ "VERTEX")
     ; Used by old POLYLINE / 3DPOLY / meshes.
     (setq newed (mrtz0:zero-point-codes newed '(10)))
    )

    ; ----- OCS / planar entities: only safe if plane is parallel to World XY -----

    ((or (= typ "ARC") (= typ "CIRCLE"))
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         (setq newed (mrtz0:zero-point-codes newed '(10)))
         (setq newed (mrtz0:zero-scalar-code newed 39))
       )
     )
    )

    ((= typ "ELLIPSE")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (setq newed (mrtz0:zero-point-codes newed '(10 11)))
     )
    )

    ((= typ "LWPOLYLINE")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         ; Elevation is group 38; XY vertices remain unchanged.
         (setq newed (mrtz0:zero-scalar-code newed 38))
         (setq newed (mrtz0:zero-scalar-code newed 39))
       )
     )
    )

    ((or (= typ "TEXT") (= typ "ATTDEF") (= typ "ATTRIB"))
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         ; 10 = insertion, 11 = alignment point.
         (setq newed (mrtz0:zero-point-codes newed '(10 11)))
         (setq newed (mrtz0:zero-scalar-code newed 39))
       )
     )
    )

    ((= typ "MTEXT")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         ; 10 = insertion point, 11 = text X-axis direction.
         (setq newed (mrtz0:zero-point-codes newed '(10 11)))
       )
     )
    )

    ((or (= typ "INSERT") (= typ "MINSERT"))
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (setq newed (mrtz0:zero-point-codes newed '(10)))
     )
    )

    ((= typ "HATCH")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       ; First 10 is elevation point; any later 3D 10 values are also zeroed.
       ; 2D boundary points are untouched.
       (setq newed (mrtz0:zero-point-codes newed '(10)))
     )
    )

    ((or (= typ "SOLID") (= typ "TRACE"))
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         (setq newed (mrtz0:zero-point-codes newed '(10 11 12 13)))
         (setq newed (mrtz0:zero-scalar-code newed 39))
       )
     )
    )

    ((= typ "SHAPE")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (progn
         (setq newed (mrtz0:zero-point-codes newed '(10)))
         (setq newed (mrtz0:zero-scalar-code newed 39))
       )
     )
    )

    ((= typ "DIMENSION")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       ; Covers rotated/aligned/angular/radial/diametric/ordinate dimension
       ; definition points without changing dimension type/style.
       (setq newed (mrtz0:zero-point-codes newed '(10 11 12 13 14 15 16)))
     )
    )

    ((= typ "TOLERANCE")
     (if (not (mrtz0:z-parallel-normal-p ed))
       (setq newed 'TILTED)
       (setq newed (mrtz0:zero-point-codes newed '(10 11)))
     )
    )

    ; POLYLINE header is handled by mrtz0:plan-polyline, not here.
    ((= typ "POLYLINE")
     (setq newed 'COMPOSITE)
    )

    ; ----- Raster IMAGE / WIPEOUT -----

    ((or (= typ "IMAGE") (= typ "WIPEOUT"))
     (setq newed 'RASTER)
    )

    ; ----- Deliberately skipped complex / external / 3D objects -----

    ((member typ
      '(
        "MLEADER"
        "PDFUNDERLAY" "DWFUNDERLAY" "DGNUNDERLAY"
        "OLE2FRAME"
        "ACAD_TABLE" "TABLE"
        "VIEWPORT"
        "HELIX"
        "3DSOLID" "BODY" "REGION"
        "SURFACE" "PLANESURFACE" "EXTRUDEDSURFACE"
        "LOFTEDSURFACE" "REVOLVEDSURFACE" "SWEPTSURFACE"
        "NURBSSURFACE"
        "ACAD_PROXY_ENTITY"
       ))
     (setq newed 'UNSUPPORTED)
    )

    (T
     (setq newed 'UNSUPPORTED)
    )
  )

  (cond
    ((eq newed 'RASTER)
     (mrtz0:plan-raster ed)
    )
    ((eq newed 'TILTED)
     (mrtz0:res-skip (strcat typ " - tilted Normal") ed)
    )
    ((eq newed 'UNSUPPORTED)
     (mrtz0:res-skip (strcat typ " - unsupported/unsafe") ed)
    )
    ((eq newed 'VERTICAL)
     (mrtz0:res-skip (strcat typ " - vertical direction cannot flatten safely") ed)
    )
    ((eq newed 'COMPOSITE)
     (mrtz0:res-skip (strcat typ " - composite handler required") ed)
    )
        ((mrtz0:changed-p ed newed)
     (mrtz0:res-ok newed)
    )
    (T
     (mrtz0:res-nochange ed)
    )
  )
)

; -------------------------
; ActiveX elevation fallback for 2D polylines
; -------------------------

(defun mrtz0:vla-elevation-nonzero-p (en / obj val)
  ; Some legacy AcDb2dPolyline objects expose Elevation reliably through
  ; ActiveX even when DXF cleanup alone does not update the displayed property.
  (setq obj (vlax-ename->vla-object en))
  (if (and obj (vlax-property-available-p obj 'Elevation))
    (progn
      (setq val (vl-catch-all-apply 'vla-get-Elevation (list obj)))
      (and
        (not (vl-catch-all-error-p val))
        (numberp val)
        (> (mrtz0:abs val) *mrtz0:tol*)
      )
    )
    nil
  )
)

(defun mrtz0:vla-force-elevation-zero (en / obj res)
  ; Returns T when writable Elevation was successfully set to 0.
  (setq obj (vlax-ename->vla-object en))
  (if (and obj (vlax-property-available-p obj 'Elevation T))
    (progn
      (setq res (vl-catch-all-apply 'vla-put-Elevation (list obj 0.0)))
      (not (vl-catch-all-error-p res))
    )
    nil
  )
)

; -------------------------
; POLYLINE composite planner/writer
; -------------------------

(defun mrtz0:poly-3d-p (ed / flags)
  (setq flags (cond ((cdr (assoc 70 ed))) (0)))
  ; 8 = 3D polyline; 16 = polygon mesh; 64 = polyface mesh
  (/= 0 (logand flags (+ 8 16 64)))
)

(defun mrtz0:plan-poly-header (ed / newed)
  (if (and
        (not (mrtz0:poly-3d-p ed))
        (not (mrtz0:z-parallel-normal-p ed)))
    (mrtz0:res-skip "POLYLINE - tilted Normal" ed)
    (progn
      ; Old 2D POLYLINE elevation is carried in header group 10 Z.
      ; For 3D/polyface/mesh this safely zeros the header reference point too.
      (setq newed (mrtz0:zero-point-codes ed '(10)))
      (setq newed (mrtz0:zero-scalar-code newed 39))
      (if (mrtz0:changed-p ed newed)
        (mrtz0:res-ok newed)
        (mrtz0:res-nochange ed)
      )
    )
  )
)

; -------------------------
; Scan/write one raw entity
; -------------------------

(defun mrtz0:inspect-result (typ res)
  (mrtz0:count-type typ)
  (cond
    ((= (car res) "OK")
     (mrtz0:count-change typ)
    )
    ((= (car res) "SKIP")
     (mrtz0:count-skip (cadr res))
    )
  )
)

(defun mrtz0:write-result (en typ res / wr)
  (mrtz0:inspect-result typ res)
  (if (= (car res) "OK")
    (progn
      (setq wr (vl-catch-all-apply 'entmod (list (cadr res))))
      (if (or (vl-catch-all-error-p wr) (null wr))
        (mrtz0:count-fail typ)
        (entupd en)
      )
    )
  )
)

(defun mrtz0:scan-raw (en / ed typ res)
  (if (and en (setq ed (entget en)))
    (progn
      (setq typ (cdr (assoc 0 ed)))
      (setq res
        (if (= typ "LWPOLYLINE")
          (mrtz0:plan-lwpoly en ed)
          (mrtz0:plan-raw ed)
        )
      )
      (mrtz0:inspect-result typ res)
    )
  )
)

(defun mrtz0:clean-raw (en / ed typ res)
  (if (and en (setq ed (entget en)))
    (progn
      (setq typ (cdr (assoc 0 ed)))
      (setq res
        (if (= typ "LWPOLYLINE")
          (mrtz0:plan-lwpoly en ed)
          (mrtz0:plan-raw ed)
        )
      )
      (mrtz0:write-result en typ res)
    )
  )
)

; -------------------------
; Composite POLYLINE
; -------------------------

(defun mrtz0:scan-polyline (en / ed res sub sed typ allowVerts vlaElevNeeds)
  (setq ed (entget en)
        res (mrtz0:plan-poly-header ed)
        vlaElevNeeds nil)

  ; Legacy 2D POLYLINE fallback: inspect ActiveX Elevation too.
  (if (not (mrtz0:poly-3d-p ed))
    (setq vlaElevNeeds (mrtz0:vla-elevation-nonzero-p en))
  )

  (mrtz0:inspect-result "POLYLINE" res)

  ; If DXF looked clean but ActiveX still reports nonzero Elevation,
  ; count it as needing cleanup.
  (if (and vlaElevNeeds (/= (car res) "OK"))
    (mrtz0:count-change "POLYLINE")
  )

  ; If a 2D POLYLINE itself is tilted, do not touch its VERTEX children.
  (setq allowVerts (not (= (car res) "SKIP")))

  (setq sub (entnext en))
  (while sub
    (setq sed (entget sub)
          typ (cdr (assoc 0 sed)))
    (cond
      ((= typ "SEQEND")
       (setq sub nil)
      )
      (T
       (if (= typ "VERTEX")
         (if allowVerts
           (mrtz0:scan-raw sub)
           (progn
             (mrtz0:count-type "VERTEX")
             (mrtz0:count-skip "VERTEX - parent POLYLINE tilted")
           )
         )
       )
       (setq sub (entnext sub))
      )
    )
  )
)

(defun mrtz0:clean-polyline (en / ed res sub sed typ allowVerts wr vlaWasNonzero vlaOk)
  (setq ed (entget en)
        res (mrtz0:plan-poly-header ed)
        vlaWasNonzero nil
        vlaOk nil)

  ; Read the legacy 2D POLYLINE Elevation before writing.
  (if (not (mrtz0:poly-3d-p ed))
    (setq vlaWasNonzero (mrtz0:vla-elevation-nonzero-p en))
  )

  (mrtz0:write-result en "POLYLINE" res)

  ; Explicit ActiveX fallback. This is the important v5 addition for
  ; old AcDb2dPolyline objects whose Properties palette still shows
  ; Elevation != 0 after DXF-only cleanup.
  (if (and
        (not (mrtz0:poly-3d-p ed))
        (/= (car res) "SKIP")
        vlaWasNonzero)
    (progn
      (setq vlaOk (mrtz0:vla-force-elevation-zero en))
      (if (not vlaOk)
        (mrtz0:count-fail "POLYLINE-ELEVATION")
      )
    )
  )

  (setq allowVerts (not (= (car res) "SKIP")))

  (setq sub (entnext en))
  (while sub
    (setq sed (entget sub)
          typ (cdr (assoc 0 sed)))
    (cond
      ((= typ "SEQEND")
       (setq sub nil)
      )
      (T
       (if (= typ "VERTEX")
         (if allowVerts
           (mrtz0:clean-raw sub)
           (progn
             (mrtz0:count-type "VERTEX")
             (mrtz0:count-skip "VERTEX - parent POLYLINE tilted")
           )
         )
       )
       (setq sub (entnext sub))
      )
    )
  )
)

; -------------------------
; INSERT attributes
; -------------------------

(defun mrtz0:insert-has-attribs-p (ed)
  (= 1 (cond ((cdr (assoc 66 ed))) (0)))
)

(defun mrtz0:scan-insert-attribs (en ed / sub sed typ)
  (if (mrtz0:insert-has-attribs-p ed)
    (progn
      (setq sub (entnext en))
      (while sub
        (setq sed (entget sub)
              typ (cdr (assoc 0 sed)))
        (cond
          ((= typ "SEQEND")
           (setq sub nil)
          )
          (T
           (if (= typ "ATTRIB")
             (mrtz0:scan-raw sub)
           )
           (setq sub (entnext sub))
          )
        )
      )
    )
  )
)

(defun mrtz0:clean-insert-attribs (en ed / sub sed typ)
  (if (mrtz0:insert-has-attribs-p ed)
    (progn
      (setq sub (entnext en))
      (while sub
        (setq sed (entget sub)
              typ (cdr (assoc 0 sed)))
        (cond
          ((= typ "SEQEND")
           (setq sub nil)
          )
          (T
           (if (= typ "ATTRIB")
             (mrtz0:clean-raw sub)
           )
           (setq sub (entnext sub))
          )
        )
      )
    )
  )
)

; -------------------------
; Selection-level dispatcher
; -------------------------

(defun mrtz0:scan-entity (en / ed typ)
  (if (and en (setq ed (entget en)))
    (progn
      (setq typ (cdr (assoc 0 ed)))
      (cond
        ((= typ "POLYLINE")
         (mrtz0:scan-polyline en)
        )
        ((or (= typ "INSERT") (= typ "MINSERT"))
         (mrtz0:scan-raw en)
         (mrtz0:scan-insert-attribs en ed)
        )
        (T
         (mrtz0:scan-raw en)
        )
      )
    )
  )
)

(defun mrtz0:clean-entity (en / ed typ)
  (if (and en (setq ed (entget en)))
    (progn
      (setq typ (cdr (assoc 0 ed)))
      (cond
        ((= typ "POLYLINE")
         (mrtz0:clean-polyline en)
        )
        ((or (= typ "INSERT") (= typ "MINSERT"))
         (mrtz0:clean-raw en)
         (mrtz0:clean-insert-attribs en ed)
        )
        (T
         (mrtz0:clean-raw en)
        )
      )
    )
  )
)

; -------------------------
; Block-definition traversal
; -------------------------

(defun mrtz0:xref-block-p (bed / flags)
  (setq flags (cond ((cdr (assoc 70 bed))) (0)))
  (/= 0 (logand flags 12)) ; xref or xref overlay
)

(defun mrtz0:block-def-scan (name / btr bed sub sed typ nested)
  (if (and name (not (member name *mrtz0:seen-blocks*)))
    (progn
      (setq *mrtz0:seen-blocks* (cons name *mrtz0:seen-blocks*))
      (setq btr (tblobjname "BLOCK" name))
      (if btr
        (progn
          (setq bed (entget btr))
          (if (mrtz0:xref-block-p bed)
            (mrtz0:count-skip (strcat "BLOCK " name " - Xref definition"))
            (progn
              (setq sub (entnext btr))
              (while sub
                (setq sed (entget sub)
                      typ (cdr (assoc 0 sed)))
                (cond
                  ((= typ "ENDBLK")
                   (setq sub nil)
                  )
                  ((= typ "POLYLINE")
                   (mrtz0:scan-polyline sub)
                   ; Jump to SEQEND, then continue after it.
                   (setq sub (entnext sub))
                   (while (and sub (/= (cdr (assoc 0 (entget sub))) "SEQEND"))
                     (setq sub (entnext sub))
                   )
                   (if sub (setq sub (entnext sub)))
                  )
                  (T
                   (mrtz0:scan-raw sub)
                   (if (= typ "INSERT")
                     (progn
                       (setq nested (cdr (assoc 2 sed)))
                       (mrtz0:block-def-scan nested)
                     )
                   )
                   (setq sub (entnext sub))
                  )
                )
              )
            )
          )
        )
      )
    )
  )
)

(defun mrtz0:block-def-clean (name / btr bed sub sed typ nested)
  (if (and name (not (member name *mrtz0:seen-blocks*)))
    (progn
      (setq *mrtz0:seen-blocks* (cons name *mrtz0:seen-blocks*))
      (setq btr (tblobjname "BLOCK" name))
      (if btr
        (progn
          (setq bed (entget btr))
          (if (mrtz0:xref-block-p bed)
            (mrtz0:count-skip (strcat "BLOCK " name " - Xref definition"))
            (progn
              (setq sub (entnext btr))
              (while sub
                (setq sed (entget sub)
                      typ (cdr (assoc 0 sed)))
                (cond
                  ((= typ "ENDBLK")
                   (setq sub nil)
                  )
                  ((= typ "POLYLINE")
                   (mrtz0:clean-polyline sub)
                   ; Jump to SEQEND, then continue after it.
                   (setq sub (entnext sub))
                   (while (and sub (/= (cdr (assoc 0 (entget sub))) "SEQEND"))
                     (setq sub (entnext sub))
                   )
                   (if sub (setq sub (entnext sub)))
                  )
                  (T
                   ; Raw traversal is intentional here: ATTRIB/ATTDEF are
                   ; handled as their own entities and are not double-processed.
                   (mrtz0:clean-raw sub)
                   (if (= typ "INSERT")
                     (progn
                       (setq nested (cdr (assoc 2 sed)))
                       (mrtz0:block-def-clean nested)
                     )
                   )
                   (setq sub (entnext sub))
                  )
                )
              )
            )
          )
        )
      )
    )
  )
)

(defun mrtz0:selected-block-defs-scan (ss / i en ed typ name)
  (setq i 0)
  (repeat (sslength ss)
    (setq en (ssname ss i)
          ed (entget en)
          typ (cdr (assoc 0 ed)))
    (if (or (= typ "INSERT") (= typ "MINSERT"))
      (progn
        (setq name (cdr (assoc 2 ed)))
        (mrtz0:block-def-scan name)
      )
    )
    (setq i (1+ i))
  )
)

(defun mrtz0:selected-block-defs-clean (ss / i en ed typ name)
  (setq i 0)
  (repeat (sslength ss)
    (setq en (ssname ss i)
          ed (entget en)
          typ (cdr (assoc 0 ed)))
    (if (or (= typ "INSERT") (= typ "MINSERT"))
      (progn
        (setq name (cdr (assoc 2 ed)))
        (mrtz0:block-def-clean name)
      )
    )
    (setq i (1+ i))
  )
)

; -------------------------
; Main scan runner
; -------------------------

(defun mrtz0:scan-selection (ss deep / i)
  (mrtz0:reset-counts)

  (setq i 0)
  (repeat (sslength ss)
    (mrtz0:scan-entity (ssname ss i))
    (setq i (1+ i))
  )

  (if deep
    (progn
      (setq *mrtz0:seen-blocks* '())
      (mrtz0:selected-block-defs-scan ss)
    )
  )
)

; -------------------------
; Main clean runner
; -------------------------

(defun mrtz0:clean-selection (ss deep / i)
  (mrtz0:reset-counts)

  (setq i 0)
  (repeat (sslength ss)
    (mrtz0:clean-entity (ssname ss i))
    (setq i (1+ i))
  )

  (if deep
    (progn
      (setq *mrtz0:seen-blocks* '())
      (mrtz0:selected-block-defs-clean ss)
    )
  )
)

; -------------------------
; Startup help / usage window
; -------------------------
; Exact Bahnschrift typography is not available through native DCL.
; This helper creates a temporary PowerShell WinForms dialog, launched
; synchronously from AutoCAD. No permanent PS1 file is installed.

(defun mrtz0:write-help-ps1 (ps1 / f)
  (setq f (open ps1 "w"))
  (if f
    (progn
      (write-line "Add-Type -AssemblyName System.Windows.Forms" f)
      (write-line "Add-Type -AssemblyName System.Drawing" f)
      (write-line "[System.Windows.Forms.Application]::EnableVisualStyles()" f)
      (write-line "function U([string]$s) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($s)) }" f)
      (write-line "$family = 'Segoe UI'" f)
      (write-line "try { $null = New-Object System.Drawing.Font($family,10) } catch { $family = 'Arial' }" f)
      (write-line "$form = New-Object System.Windows.Forms.Form" f)
      (write-line "$form.Text = 'Z0Clean v2.2'" f)
      (write-line "$form.ClientSize = New-Object System.Drawing.Size(920,720)" f)
      (write-line "$form.StartPosition = 'CenterScreen'" f)
      (write-line "$form.FormBorderStyle = 'FixedDialog'" f)
      (write-line "$form.MaximizeBox = $false" f)
      (write-line "$form.MinimizeBox = $false" f)
      (write-line "$form.ShowInTaskbar = $true" f)
      (write-line "$form.TopMost = $true" f)
      (write-line "$form.BackColor = [System.Drawing.Color]::FromArgb(37,39,43)" f)
      (write-line "$form.ForeColor = [System.Drawing.Color]::FromArgb(242,243,245)" f)
      (write-line "$form.Font = New-Object System.Drawing.Font($family,11,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$title = New-Object System.Windows.Forms.Label" f)
      (write-line "$title.Text = U('WjBDbGVhbg==')" f)
      (write-line "$title.Font = New-Object System.Drawing.Font($family,29,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$title.AutoSize = $true" f)
      (write-line "$title.Location = New-Object System.Drawing.Point(46,28)" f)
      (write-line "$form.Controls.Add($title)" f)
      (write-line "$ver = New-Object System.Windows.Forms.Label" f)
      (write-line "$ver.Text = U('djIuMg==')" f)
      (write-line "$ver.Font = New-Object System.Drawing.Font($family,11,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$ver.ForeColor = [System.Drawing.Color]::FromArgb(158,196,247)" f)
      (write-line "$ver.AutoSize = $true" f)
      (write-line "$ver.Location = New-Object System.Drawing.Point(214,55)" f)
      (write-line "$form.Controls.Add($ver)" f)
      (write-line "$sub = New-Object System.Windows.Forms.Label" f)
      (write-line "$sub.Text = U('TMOgbSBz4bqhY2ggY2FvIMSR4buZIFogY2hvIGLhuqNuIHbhur0gaG/DoG4gY8O0bmcgMkQ=')" f)
      (write-line "$sub.Font = New-Object System.Drawing.Font($family,13,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$sub.ForeColor = [System.Drawing.Color]::FromArgb(188,194,203)" f)
      (write-line "$sub.AutoSize = $true" f)
      (write-line "$sub.Location = New-Object System.Drawing.Point(50,94)" f)
      (write-line "$form.Controls.Add($sub)" f)
      (write-line "$sep = New-Object System.Windows.Forms.Panel" f)
      (write-line "$sep.BackColor = [System.Drawing.Color]::FromArgb(72,76,83)" f)
      (write-line "$sep.Location = New-Object System.Drawing.Point(50,134)" f)
      (write-line "$sep.Size = New-Object System.Drawing.Size(820,1)" f)
      (write-line "$form.Controls.Add($sep)" f)
      (write-line "$heading = New-Object System.Windows.Forms.Label" f)
      (write-line "$heading.Text = U('Q8OBQ0ggU+G7rCBE4bukTkc=')" f)
      (write-line "$heading.Font = New-Object System.Drawing.Font($family,16,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$heading.AutoSize = $true" f)
      (write-line "$heading.Location = New-Object System.Drawing.Point(50,157)" f)
      (write-line "$form.Controls.Add($heading)" f)
      (write-line "$p1 = New-Object System.Windows.Forms.Panel" f)
      (write-line "$p1.Location = New-Object System.Drawing.Point(50,203)" f)
      (write-line "$p1.Size = New-Object System.Drawing.Size(390,150)" f)
      (write-line "$p1.BackColor = [System.Drawing.Color]::FromArgb(43,46,51)" f)
      (write-line "$form.Controls.Add($p1)" f)
      (write-line "$p1t = New-Object System.Windows.Forms.Label" f)
      (write-line "$p1t.Text = U('Q8OBQ0ggMSDigJQgR8OVIEzhu4ZOSCBUUsav4buaQw==')" f)
      (write-line "$p1t.Font = New-Object System.Drawing.Font($family,12.5,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$p1t.ForeColor = [System.Drawing.Color]::FromArgb(170,205,255)" f)
      (write-line "$p1t.AutoSize = $true" f)
      (write-line "$p1t.Location = New-Object System.Drawing.Point(18,17)" f)
      (write-line "$p1.Controls.Add($p1t)" f)
      (write-line "$p1b = New-Object System.Windows.Forms.Label" f)
      (write-line "$p1b.Text = U('MS4gR8O1IFowQ0xFQU4g4oaSIEVudGVyDQoyLiBDaOG7jW4gY8OhYyBvYmplY3QgY+G6p24gxJHGsGEgduG7gSBaID0gMCDihpIgRW50ZXI=')" f)
      (write-line "$p1b.Font = New-Object System.Drawing.Font($family,11.5,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$p1b.ForeColor = [System.Drawing.Color]::FromArgb(224,227,231)" f)
      (write-line "$p1b.Location = New-Object System.Drawing.Point(18,52)" f)
      (write-line "$p1b.Size = New-Object System.Drawing.Size(350,78)" f)
      (write-line "$p1.Controls.Add($p1b)" f)
      (write-line "$p2 = New-Object System.Windows.Forms.Panel" f)
      (write-line "$p2.Location = New-Object System.Drawing.Point(460,203)" f)
      (write-line "$p2.Size = New-Object System.Drawing.Size(410,150)" f)
      (write-line "$p2.BackColor = [System.Drawing.Color]::FromArgb(43,46,51)" f)
      (write-line "$form.Controls.Add($p2)" f)
      (write-line "$p2t = New-Object System.Windows.Forms.Label" f)
      (write-line "$p2t.Text = U('Q8OBQ0ggMiDigJQgQ0jhu4xOIE9CSkVDVCBUUsav4buaQw==')" f)
      (write-line "$p2t.Font = New-Object System.Drawing.Font($family,12.5,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$p2t.ForeColor = [System.Drawing.Color]::FromArgb(170,205,255)" f)
      (write-line "$p2t.AutoSize = $true" f)
      (write-line "$p2t.Location = New-Object System.Drawing.Point(18,17)" f)
      (write-line "$p2.Controls.Add($p2t)" f)
      (write-line "$p2b = New-Object System.Windows.Forms.Label" f)
      (write-line "$p2b.Text = U('MS4gQ2jhu41uIHPhurVuIGPDoWMgb2JqZWN0IGPhuqduIHjhu60gbMO9DQoyLiBHw7UgWjBDTEVBTiDihpIgRW50ZXI=')" f)
      (write-line "$p2b.Font = New-Object System.Drawing.Font($family,11.5,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$p2b.ForeColor = [System.Drawing.Color]::FromArgb(224,227,231)" f)
      (write-line "$p2b.Location = New-Object System.Drawing.Point(18,52)" f)
      (write-line "$p2b.Size = New-Object System.Drawing.Size(370,78)" f)
      (write-line "$p2.Controls.Add($p2b)" f)
      (write-line "$common = New-Object System.Windows.Forms.Panel" f)
      (write-line "$common.Location = New-Object System.Drawing.Point(50,373)" f)
      (write-line "$common.Size = New-Object System.Drawing.Size(820,168)" f)
      (write-line "$common.BackColor = [System.Drawing.Color]::FromArgb(41,44,49)" f)
      (write-line "$form.Controls.Add($common)" f)
      (write-line "$ct = New-Object System.Windows.Forms.Label" f)
      (write-line "$ct.Text = U('U0FVIMSQw5M=')" f)
      (write-line "$ct.Font = New-Object System.Drawing.Font($family,13,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$ct.ForeColor = [System.Drawing.Color]::FromArgb(242,243,245)" f)
      (write-line "$ct.AutoSize = $true" f)
      (write-line "$ct.Location = New-Object System.Drawing.Point(18,15)" f)
      (write-line "$common.Controls.Add($ct)" f)
      (write-line "$cb = New-Object System.Windows.Forms.Label" f)
      (write-line "$cb.Text = U('My4gQ2jhu41uIFllcy9ObyBu4bq/dSBtdeG7kW4geOG7rSBsw70gY+G6oyBCbG9jayBEZWZpbml0aW9uDQo0LiBaMENsZWFuIHThu7EgU0NBTiB2w6AgaGnhu4duIGLDoW8gY8Ohbw0KNS4gQ2jhu41uIFllcyDEkeG7gyBDTEVBTg==')" f)
      (write-line "$cb.Font = New-Object System.Drawing.Font($family,11.5,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$cb.ForeColor = [System.Drawing.Color]::FromArgb(218,221,226)" f)
      (write-line "$cb.Location = New-Object System.Drawing.Point(20,48)" f)
      (write-line "$cb.Size = New-Object System.Drawing.Size(770,92)" f)
      (write-line "$common.Controls.Add($cb)" f)
      (write-line "$note = New-Object System.Windows.Forms.Label" f)
      (write-line "$note.Text = U('WjBTQ0FOOiBjaOG7iSBraeG7g20gdHJhLCBraMO0bmcgdGhheSDEkeG7lWkgYuG6o24gduG6vS4gICBO4bq/dSBj4bqnbiBob8OgbiB0w6FjLCBkw7luZyBVTkRPIG3hu5l0IGzhuqduIG5nYXkgc2F1IFowQ2xlYW4u')" f)
      (write-line "$note.Font = New-Object System.Drawing.Font($family,10.5,[System.Drawing.FontStyle]::Italic)" f)
      (write-line "$note.ForeColor = [System.Drawing.Color]::FromArgb(168,174,183)" f)
      (write-line "$note.AutoSize = $true" f)
      (write-line "$note.Location = New-Object System.Drawing.Point(52,558)" f)
      (write-line "$form.Controls.Add($note)" f)
      (write-line "$safePanel = New-Object System.Windows.Forms.Panel" f)
      (write-line "$safePanel.BackColor = [System.Drawing.Color]::FromArgb(43,47,52)" f)
      (write-line "$safePanel.Location = New-Object System.Drawing.Point(50,590)" f)
      (write-line "$safePanel.Size = New-Object System.Drawing.Size(820,42)" f)
      (write-line "$form.Controls.Add($safePanel)" f)
      (write-line "$safe = New-Object System.Windows.Forms.Label" f)
      (write-line "$safe.Text = U('S2jDtG5nIEVYUExPREUgIOKAoiAgS2jDtG5nIMOpcCBOb3JtYWwgMjEwICDigKIgIE9iamVjdCBraMO0bmcgYW4gdG/DoG4gc+G6vSBTS0lQ')" f)
      (write-line "$safe.Font = New-Object System.Drawing.Font($family,10.5,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$safe.ForeColor = [System.Drawing.Color]::FromArgb(180,204,235)" f)
      (write-line "$safe.AutoSize = $true" f)
      (write-line "$safe.Location = New-Object System.Drawing.Point(14,11)" f)
      (write-line "$safePanel.Controls.Add($safe)" f)
      (write-line "$footer = New-Object System.Windows.Forms.Label" f)
      (write-line "$footer.Text = U('V29ya2Zsb3c6IE1yLlRvYW4tTmhpVFc=')" f)
      (write-line "$footer.Font = New-Object System.Drawing.Font($family,9.5,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$footer.ForeColor = [System.Drawing.Color]::FromArgb(126,131,139)" f)
      (write-line "$footer.AutoSize = $true" f)
      (write-line "$footer.Location = New-Object System.Drawing.Point(52,675)" f)
      (write-line "$form.Controls.Add($footer)" f)
      (write-line "$run = New-Object System.Windows.Forms.Button" f)
      (write-line "$run.Text = U('Q0jhuqBZIFowQ0xFQU4=')" f)
      (write-line "$run.Font = New-Object System.Drawing.Font($family,11.5,[System.Drawing.FontStyle]::Bold)" f)
      (write-line "$run.Size = New-Object System.Drawing.Size(190,46)" f)
      (write-line "$run.Location = New-Object System.Drawing.Point(540,655)" f)
      (write-line "$run.FlatStyle = 'Flat'" f)
      (write-line "$run.FlatAppearance.BorderSize = 0" f)
      (write-line "$run.BackColor = [System.Drawing.Color]::FromArgb(0,120,212)" f)
      (write-line "$run.ForeColor = [System.Drawing.Color]::White" f)
      (write-line "$run.Cursor = [System.Windows.Forms.Cursors]::Hand" f)
      (write-line "$run.Add_Click({ $form.Tag = 2; $form.Close() })" f)
      (write-line "$form.Controls.Add($run)" f)
      (write-line "$close = New-Object System.Windows.Forms.Button" f)
      (write-line "$close.Text = U('xJDDk05H')" f)
      (write-line "$close.Font = New-Object System.Drawing.Font($family,11,[System.Drawing.FontStyle]::Regular)" f)
      (write-line "$close.Size = New-Object System.Drawing.Size(125,46)" f)
      (write-line "$close.Location = New-Object System.Drawing.Point(745,655)" f)
      (write-line "$close.FlatStyle = 'Flat'" f)
      (write-line "$close.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(88,92,99)" f)
      (write-line "$close.FlatAppearance.BorderSize = 1" f)
      (write-line "$close.BackColor = [System.Drawing.Color]::FromArgb(47,50,55)" f)
      (write-line "$close.ForeColor = [System.Drawing.Color]::FromArgb(232,234,237)" f)
      (write-line "$close.Cursor = [System.Windows.Forms.Cursors]::Hand" f)
      (write-line "$close.Add_Click({ $form.Tag = 0; $form.Close() })" f)
      (write-line "$form.Controls.Add($close)" f)
      (write-line "$form.AcceptButton = $run" f)
      (write-line "$form.CancelButton = $close" f)
      (write-line "$form.Add_Shown({ $form.Activate() })" f)
      (write-line "$form.Tag = 0" f)
      (write-line "[void]$form.ShowDialog()" f)
      (write-line "if ($form.Tag -eq 2) { exit 2 } else { exit 0 }" f)
      (close f)
      T
    )
    nil
  )
)
(defun mrtz0:show-help-window (/ tmp ps1 shell cmd rc)
  (setq tmp (getenv "TEMP"))
  (if (null tmp) (setq tmp "."))
  (setq ps1 (strcat tmp "\\Z0SAFE_Help.ps1"))

  (if (mrtz0:write-help-ps1 ps1)
    (progn
      (setq shell (vl-catch-all-apply 'vlax-create-object (list "WScript.Shell")))
      (if (vl-catch-all-error-p shell)
        (progn
          (alert
            (strcat
              "Z0Clean v2.2\n\n"
              "Z0CLEAN = SCAN + CLEAN.\n"
              "1. Chon object can dua ve Z=0.\n"
              "2. Chon Yes neu can clean ben trong Block Definition.\n"
              "3. Xem bao cao scan, sau do xac nhan CLEAN.\n\n"
              "Z0SCAN chi dung de kiem tra, khong sua.\n\n"
              "Workflow: Mr.Toan-NhiTW."
            )
          )
          (setq rc 0)
        )
        (progn
          (setq cmd
            (strcat
              "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "
              "\"" ps1 "\""
            )
          )
          (setq rc
            (vl-catch-all-apply
              'vlax-invoke-method
              (list shell 'Run cmd 0 :vlax-true)
            )
          )
          (vlax-release-object shell)
          (if (vl-catch-all-error-p rc)
            (setq rc 0)
          )
        )
      )
      (vl-file-delete ps1)
    )
    (setq rc 0)
  )
  rc
)

(defun mrtz0:show-help-and-run (/ rc)
  (setq rc (mrtz0:show-help-window))
  (if (= rc 2)
    (c:MRTZ0CLEAN)
  )
  (princ)
)

(defun c:MRTZ0HELP ()
  (mrtz0:show-help-and-run)
)

(defun c:MRTZ0 ()
  (c:MRTZ0CLEAN)
)

; -------------------------
; User prompts
; -------------------------

(defun mrtz0:get-deep-option (/ ans)
  (initget "Yes No")
  (setq ans
    (getkword
      "\nScan/clean geometry INSIDE selected block definitions? [Yes/No] <No>: "
    )
  )
  (= ans "Yes")
)

(defun mrtz0:get-selection (/ ss implied)
  ; Human-friendly selection:
  ; - If objects were selected before running Z0CLEAN, use PICKFIRST selection.
  ; - Otherwise prompt for a normal selection.
  (setq implied (ssget "_I"))
  (if implied
    (progn
      (setq ss implied)
      (princ
        (strcat
          "\nUsing preselected objects: "
          (itoa (sslength ss))
        )
      )
    )
    (progn
      (princ "\nSelect objects to scan/clean for Z=0: ")
      (setq ss (ssget))
    )
  )
  ss
)

; -------------------------
; Command: Z0SCAN
; -------------------------

(defun c:MRTZ0SCAN (/ ss deep)
  (setq ss (mrtz0:get-selection))
  (if ss
    (progn
      (setq deep (mrtz0:get-deep-option))
      (mrtz0:scan-selection ss deep)
      (mrtz0:print-summary "SCAN")
      (if deep
        (princ
          "\nNOTE: Block definitions were only scanned; nothing was changed."
        )
      )
    )
    (princ "\nNothing selected.")
  )
  (princ)
)

; -------------------------
; Command: Z0CLEAN
; -------------------------

(defun c:MRTZ0CLEAN (/ *error* doc ss deep ans oldcmdecho)
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object))
        oldcmdecho (getvar "CMDECHO"))

  (defun *error* (msg)
    (if doc
      (vl-catch-all-apply 'vla-EndUndoMark (list doc))
    )
    (setvar "CMDECHO" oldcmdecho)
    (if (and msg
             (/= msg "Function cancelled")
             (/= msg "quit / exit abort"))
      (princ (strcat "\nZ0Clean error: " msg))
    )
    (princ)
  )

  (setq ss (mrtz0:get-selection))

  (if ss
    (progn
      (setq deep (mrtz0:get-deep-option))

      ; First pass = scan only.
      (mrtz0:scan-selection ss deep)
      (mrtz0:print-summary "PRE-CLEAN SCAN")

      (princ
        "\n\nSafety rule: tilted OCS entities and complex/unsafe entities will be SKIPPED."
      )
      (princ
        "\nDXF Normal 210 is NEVER rewritten."
      )

      (initget "Yes No")
      (setq ans
        (getkword
          "\nProceed with safe Z cleanup? [Yes/No] <No>: "
        )
      )

      (if (= ans "Yes")
        (progn
          (setvar "CMDECHO" 0)
          (vla-StartUndoMark doc)

          (mrtz0:clean-selection ss deep)

          (vla-EndUndoMark doc)
          (setvar "CMDECHO" oldcmdecho)

          (command "_.REGENALL")
          (mrtz0:print-summary "CLEAN RESULT")

          (if deep
            (princ
              "\nWARNING: Block definitions were modified; all inserts of those definitions are affected."
            )
          )

          (princ
            "\nUse UNDO once to revert the entire Z0CLEAN operation if needed."
          )
        )
        (princ "\nNo changes made.")
      )
    )
    (princ "\nNothing selected.")
  )
  (princ)
)

(princ "\nZ0Clean v2.2 loaded.")
(princ "\nCommands: MRTZ0 / MRTZ0CLEAN = scan + clean, MRTZ0SCAN = scan, MRTZ0HELP = help.")
(princ)
