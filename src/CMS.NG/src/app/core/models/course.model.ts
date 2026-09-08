import { CertificationLookup } from './certification.model';
import { JobCategoryLookup } from './job-category.model';

/** 原廠 — slim FK reference, mirrors CMS.API `CoursePartnerRef`. */
export interface CoursePartnerRef {
  pkid: number;
  name: string;
}

/** 課程群組 — slim FK reference, mirrors CMS.API `CourseGroupRef`. */
export interface CourseGroupRef {
  pkid: number;
  description: string;
}

/** 上架狀態 — slim FK reference, mirrors CMS.API `CoursePublishStatusRef`. */
export interface CoursePublishStatusRef {
  pkid: number;
  description: string;
}

/** 課程 Course — response model, mirrors CMS.API `Course`. */
export interface Course {
  /** 主代碼 — int IDENTITY, assigned by the database */
  pkid: number;
  /** 課程名稱 */
  title: string;
  /** 官方課程名稱 — nullable */
  officialTitle: string | null;
  /** 簡介代碼 */
  courseId: string;
  /** 科目代碼 */
  prodCourseId: string;
  /** 網址代稱 */
  friendlyUrl: string;
  /** 顯示順序 */
  displayOrder: number;
  /** 原廠 — FK key */
  partnerPkid: number;
  /** 課程群組 — FK key, nullable */
  courseGroupPkid: number | null;
  /** 上架狀態 — FK key */
  publishStatusPkid: number;
  /** 上架日期 — ISO `yyyy-MM-dd` */
  scheduleOn: string;
  /** 下架日期 — ISO `yyyy-MM-dd` */
  scheduleOff: string;
  /** 時數 */
  hour: number;
  /** 定價 — decimal(9,0) */
  listPrice: number;
  /** 點數 — decimal(9,1) */
  learningCredit: number;
  /** 教材 — nullable */
  material: string | null;
  /** 課程目標 — nullable */
  objective: string | null;
  /** 適合對象 — nullable */
  target: string | null;
  /** 先備知識 — nullable */
  prerequisites: string | null;
  /** 課程大綱 — nullable */
  outline: string | null;
  /** 對應認證／考試 — nullable */
  towardCertOrExam: string | null;
  /** 備註 — nullable */
  note: string | null;
  /** 其他資訊 — nullable */
  otherInfo: string | null;
  /** 允許重聽 */
  canRepeat: boolean;
  /** 原廠 — nav object */
  partner: CoursePartnerRef | null;
  /** 課程群組 — nav object, null when the FK is null */
  courseGroup: CourseGroupRef | null;
  /** 上架狀態 — nav object */
  publishStatus: CoursePublishStatusRef | null;
  /** 職務類別數 */
  jobCategoryCount: number;
  /** 對應認證數 */
  certificationCount: number;
  /** 職務類別 — populated on single-record reads only */
  jobCategories: JobCategoryLookup[];
  /** 對應認證 — populated on single-record reads only */
  certifications: CertificationLookup[];
}

/** 課程 Course — create DTO. `pkid` is IDENTITY, so it is not sent. */
export interface CourseRequest {
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  /** ISO `yyyy-MM-dd` */
  scheduleOn: string;
  /** ISO `yyyy-MM-dd` */
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  /** n-n keys for CourseJobCategories */
  jobCategoryPkids: number[];
  /** n-n keys for CourseInCertification */
  certificationPkids: number[];
}

/** 課程 Course — update DTO. PUT posts to the collection route, so the key travels in the body. */
export interface CourseUpdateRequest extends CourseRequest {
  pkid: number;
}

/** 課程 Course — search DTO. */
export interface CourseQuery {
  keyword?: string | null;
  partnerPkid?: number | null;
  courseGroupPkid?: number | null;
  publishStatusPkid?: number | null;
  /** 允許重聽 — tri-state: null is unfiltered */
  canRepeat?: boolean | null;
  /** ISO `yyyy-MM-dd`, inclusive */
  scheduleOnFrom?: string | null;
  scheduleOnTo?: string | null;
  scheduleOffFrom?: string | null;
  scheduleOffTo?: string | null;
}

/** A CourseQuery with every filter cleared. */
export const EMPTY_COURSE_QUERY: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  canRepeat: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
};
