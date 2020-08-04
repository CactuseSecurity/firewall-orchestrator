#!/bin/bash

#select mgm_name,mgm_id, max(stop_time) as last_import,cast (now() as date) - cast(max(stop_time) as date) as days_ago from import_control left join management using (mgm_id) WHERE cast (now() as date) - cast((stop_time) as date)>$max_days_since_last_import group by mgm_id,mgm_name order by mgm_id,last_import
# UNUSED? max_days_since_last_import=730

while true
do
   no_of_mgmgs=$(psql -qtAX -d isodb -c "select count(*) from management ")
   no_of_mgmgs_unwanted=$(psql -qtAX -d isodb -c "select count(*) from management where do_not_import and hide_in_gui")
   mgm_to_delete=$(psql -qtAX -d isodb -c "select mgm_name from management left join import_control using (mgm_id) where do_not_import and hide_in_gui group by mgm_id order by count(control_id) limit 1")
   echo "total number of managements: $no_of_mgmgs, remaining managements to remove: $no_of_mgmgs_unwanted, next management to remove: $mgm_to_delete"
   if [ "$mgm_to_delete" == "" ]
   then
      echo "No more managements to remove. Exiting."
      exit 0
   fi
   echo "next mgm to delete: $mgm_to_delete" 
   echo "executing: time psql -qtAX -d isodb -c delete from management where mgm_name=$mgm_to_delete"
   time psql -d isodb -c "delete from management where mgm_name='$mgm_to_delete'"
done

