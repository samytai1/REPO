/** 職務類別 JobCategory — slim lookup row used for the n-n CourseJobCategories option list. */
export interface JobCategoryLookup {
  /** 主代碼 */
  pkid: number;
  /** 類別說明 */
  description: string;
}
