/** 認證 Certification — slim lookup row used for the n-n CourseInCertification option list. */
export interface CertificationLookup {
  /** 主代碼 */
  pkid: number;
  /** 認證名稱 — nchar in the database and nullable, so it arrives RTRIMmed or null */
  title: string | null;
  /** 原廠 */
  partnerPkid: number;
}

/** The option label for a certification; a null title still needs a selectable label. */
export function certificationLabel(certification: CertificationLookup): string {
  return certification.title ?? `(未命名 #${certification.pkid})`;
}
